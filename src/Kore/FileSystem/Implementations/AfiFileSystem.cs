﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kontract;
using Kontract.Extensions;
using Kontract.Interfaces.FileSystem;
using Kontract.Interfaces.Managers;
using Kontract.Interfaces.Plugins.State;
using Kontract.Interfaces.Plugins.State.Archive;
using Kontract.Interfaces.Providers;
using Kontract.Models.Archive;
using Kontract.Models.IO;
using Kore.Streams;

namespace Kore.FileSystem.Implementations
{
    /// <summary>
    /// Provides a <see cref="IFileSystem"/> for an <see cref="IArchiveState"/>.
    /// </summary>
    class AfiFileSystem : FileSystem
    {
        private readonly IStateInfo _stateInfo;
        private readonly ITemporaryStreamProvider _temporaryStreamProvider;

        private readonly IDictionary<UPath, ArchiveFileInfo> _fileDictionary;
        private readonly IDictionary<UPath, (IList<UPath>, IList<ArchiveFileInfo>)> _directoryDictionary;

        protected IArchiveState ArchiveState => _stateInfo.PluginState as IArchiveState;

        protected UPath SubPath => _stateInfo.AbsoluteDirectory / _stateInfo.FilePath.ToRelative();

        /// <summary>
        /// Creates a new instance of <see cref="AfiFileSystem"/>.
        /// </summary>
        /// <param name="stateInfo">The <see cref="IStateInfo"/> to retrieve files from.</param>
        /// <param name="streamManager">The stream manager to scope streams in.</param>
        public AfiFileSystem(IStateInfo stateInfo, IStreamManager streamManager) : base(streamManager)
        {
            ContractAssertions.IsNotNull(stateInfo, nameof(stateInfo));
            if (!(stateInfo.PluginState is IArchiveState))
                throw new InvalidOperationException("The state is no archive.");

            _stateInfo = stateInfo;
            _temporaryStreamProvider = streamManager.CreateTemporaryStreamProvider();

            _fileDictionary = ArchiveState.Files.ToDictionary(x => x.FilePath, y => y);
            _directoryDictionary = CreateDirectoryLookup();
        }

        /// <inheritdoc />
        public override IFileSystem Clone(IStreamManager streamManager)
        {
            return new AfiFileSystem(_stateInfo, streamManager);
        }

        // ----------------------------------------------
        // Directory API
        // ----------------------------------------------

        /// <inheritdoc />
        public override bool CanCreateDirectories => false;

        /// <inheritdoc />
        public override bool CanDeleteDirectories => ArchiveState is IRemoveFiles;

        /// <inheritdoc />
        public override bool CanMoveDirectories => ArchiveState is IRenameFiles;

        /// <inheritdoc />
        protected override void CreateDirectoryImpl(UPath path)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override bool DirectoryExistsImpl(UPath path)
        {
            return _directoryDictionary.ContainsKey(path);
        }

        /// <inheritdoc />
        protected override void MoveDirectoryImpl(UPath srcPath, UPath destPath)
        {
            if (!DirectoryExists(srcPath))
            {
                throw FileSystemExceptionHelper.NewDirectoryNotFoundException(srcPath);
            }

            var element = _directoryDictionary[srcPath];

            // Move sub directories
            foreach (var subDir in element.Item1.ToArray())
                MoveDirectoryImpl(subDir, destPath / subDir.GetName());

            // Move directory
            _directoryDictionary.Remove(srcPath);

            var parent = srcPath.GetDirectory();
            if (!parent.IsNull && !parent.IsEmpty)
                _directoryDictionary[parent].Item1.Remove(srcPath);

            CreateDirectoryEntries(destPath);

            // Move files
            var renameState = ArchiveState as IRenameFiles;
            foreach (var file in element.Item2)
            {
                renameState?.Rename(file, destPath / file.FilePath.GetName());
                _directoryDictionary[destPath].Item2.Add(file);
            }
        }

        /// <inheritdoc />
        protected override void DeleteDirectoryImpl(UPath path, bool isRecursive)
        {
            if (!DirectoryExistsImpl(path))
            {
                throw FileSystemExceptionHelper.NewDirectoryNotFoundException(path);
            }

            if (!isRecursive && _directoryDictionary[path].Item1.Any())
            {
                throw FileSystemExceptionHelper.NewDirectoryIsNotEmpty(path);
            }

            var element = _directoryDictionary[path];

            // Delete sub directories
            foreach (var subDir in element.Item1.ToArray())
                DeleteDirectoryImpl(subDir, true);  // Removing sub directories is always recursive

            // Delete directory
            _directoryDictionary.Remove(path);

            var parent = path.GetDirectory();
            if (!parent.IsNull && !parent.IsEmpty)
                _directoryDictionary[parent].Item1.Remove(path);

            // Delete files
            var removeState = ArchiveState as IRemoveFiles;
            foreach (var file in element.Item2)
                removeState?.RemoveFile(file);

            element.Item2.Clear();
        }

        // ----------------------------------------------
        // File API
        // ----------------------------------------------

        /// <inheritdoc />
        public override bool CanCreateFiles => ArchiveState is IAddFiles;

        /// <inheritdoc />
        // TODO: Maybe finding out how to properly do copying when AFI can either return a normal stream or a temporary one
        public override bool CanCopyFiles => false;

        /// <inheritdoc />
        // TODO: Maybe finding out how to properly do replacing when AFI can either return a normal stream or a temporary one
        public override bool CanReplaceFiles => false;

        /// <inheritdoc />
        public override bool CanMoveFiles => ArchiveState is IRenameFiles;

        /// <inheritdoc />
        public override bool CanDeleteFiles => ArchiveState is IRemoveFiles;

        /// <inheritdoc />
        protected override bool FileExistsImpl(UPath path)
        {
            return _fileDictionary.ContainsKey(path);
        }

        /// <inheritdoc />
        protected override void CopyFileImpl(UPath srcPath, UPath destPath, bool overwrite)
        {
            // TODO: Implement copying files
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override void ReplaceFileImpl(UPath srcPath, UPath destPath, UPath destBackupPath, bool ignoreMetadataErrors)
        {
            // TODO: Implement replacing files
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        protected override long GetFileLengthImpl(UPath path)
        {
            if (!FileExistsImpl(path))
            {
                throw FileSystemExceptionHelper.NewFileNotFoundException(path);
            }

            return _fileDictionary[path].FileSize;
        }

        /// <inheritdoc />
        protected override void MoveFileImpl(UPath srcPath, UPath destPath)
        {
            if (!FileExistsImpl(srcPath))
            {
                throw FileSystemExceptionHelper.NewFileNotFoundException(srcPath);
            }

            var file = _fileDictionary[srcPath];

            // Remove file from source directory
            var srcDir = srcPath.GetDirectory();
            _directoryDictionary[srcDir].Item2.Remove(file);

            // Rename file
            var renameState = ArchiveState as IRenameFiles;
            renameState?.Rename(file, destPath);

            // Create directory of destination
            CreateDirectoryEntries(destPath.GetDirectory());

            // Add file to destination directory
            _directoryDictionary[destPath.GetDirectory()].Item2.Add(file);
        }

        /// <inheritdoc />
        protected override void DeleteFileImpl(UPath path)
        {
            if (!FileExistsImpl(path))
            {
                throw FileSystemExceptionHelper.NewFileNotFoundException(path);
            }

            var file = _fileDictionary[path];

            // Remove file from directory
            var srcDir = path.GetDirectory();
            _directoryDictionary[srcDir].Item2.Remove(file);

            // Remove file
            var removingState = ArchiveState as IRemoveFiles;
            removingState?.RemoveFile(_fileDictionary[path]);
        }

        /// <inheritdoc />
        protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share)
        {
            return OpenFileAsyncImpl(path, mode, access, share).Result;
        }

        /// <inheritdoc />
        protected override async Task<Stream> OpenFileAsyncImpl(UPath path, FileMode mode, FileAccess access, FileShare share)
        {
            if (!FileExistsImpl(path))
            {
                throw FileSystemExceptionHelper.NewFileNotFoundException(path);
            }

            // Ignore file mode, access and share for now
            // TODO: Find a way to somehow allow for mode and access to have an effect?

            // Get data of ArchiveFileInfo
            var afi = _fileDictionary[path];
            var afiData = await afi.GetFileData(_temporaryStreamProvider);

            // Wrap data accordingly to not dispose the original ArchiveFileInfo data
            if (!(afiData is TemporaryStream))
                afiData = StreamManager.WrapUndisposable(afiData);

            afiData.Position = 0;
            return afiData;
        }

        /// <inheritdoc />
        protected override void SetFileDataImpl(UPath savePath, Stream saveData)
        {
            if (!FileExistsImpl(savePath))
            {
                throw FileSystemExceptionHelper.NewFileNotFoundException(savePath);
            }

            _fileDictionary[savePath].SetFileData(saveData);
        }

        // ----------------------------------------------
        // Metadata API
        // ----------------------------------------------

        /// <inheritdoc />
        protected override ulong GetTotalSizeImpl(UPath directoryPath)
        {
            if (!DirectoryExistsImpl(directoryPath))
            {
                FileSystemExceptionHelper.NewDirectoryNotFoundException(directoryPath);
            }

            var (directories, files) = _directoryDictionary[directoryPath];

            var totalFileSize = files.Sum(x => x.FileSize);
            var totalDirectorySize = directories.Select(GetTotalSizeImpl).Sum(x => (long)x);
            return (ulong)(totalFileSize + totalDirectorySize);
        }

        /// <inheritdoc />
        protected override IEnumerable<UPath> EnumeratePathsImpl(UPath path, string searchPattern, SearchOption searchOption, SearchTarget searchTarget)
        {
            var search = SearchPattern.Parse(ref path, ref searchPattern);

            var onlyTopDirectory = searchOption == SearchOption.TopDirectoryOnly;
            var enumerateDirectories = searchTarget == SearchTarget.Directory;
            var enumerateFiles = searchTarget == SearchTarget.File;

            foreach (var enumeratedPath in EnumeratePathsInternal(path, search, enumerateDirectories, enumerateFiles, onlyTopDirectory).OrderBy(x => x))
                yield return enumeratedPath;
        }

        protected override string ConvertPathToInternalImpl(UPath path)
        {
            var safePath = path.ToRelative();
            return (SubPath / safePath).FullName;
        }

        protected override UPath ConvertPathFromInternalImpl(string innerPath)
        {
            var fullPath = innerPath;
            if (!fullPath.StartsWith(SubPath.FullName) || (fullPath.Length > SubPath.FullName.Length && fullPath[SubPath == UPath.Root ? 0 : SubPath.FullName.Length] != UPath.DirectorySeparator))
            {
                // More a safe guard, as it should never happen, but if a delegate filesystem doesn't respect its root path
                // we are throwing an exception here
                throw new InvalidOperationException($"The path `{innerPath}` returned by the delegate filesystem is not rooted to the subpath `{SubPath}`");
            }

            var subPath = fullPath.Substring(SubPath.FullName.Length);
            return subPath == string.Empty ? UPath.Root : new UPath(subPath, true);
        }

        #region Enumerating Paths

        private IEnumerable<UPath> EnumeratePathsInternal(UPath path, SearchPattern searchPattern, bool enumerateDirectories, bool enumerateFiles, bool onlyTopDirectory)
        {
            if (!DirectoryExistsImpl(path))
            {
                FileSystemExceptionHelper.NewDirectoryNotFoundException(path);
            }

            var (directories, files) = _directoryDictionary[path];

            // Enumerate files
            if (enumerateFiles)
            {
                foreach (var file in files.Where(x => searchPattern.Match(x.FilePath)))
                    yield return file.FilePath;
            }

            // Enumerate directory
            if (enumerateDirectories && searchPattern.Match(path))
                yield return path;

            // Loop through sub directories
            if (!onlyTopDirectory)
            {
                foreach (var directory in directories.Where(searchPattern.Match))
                    foreach (var enumeratedPath in EnumeratePathsInternal(directory, searchPattern, enumerateDirectories, enumerateFiles, false))
                        yield return enumeratedPath;
            }
        }

        #endregion

        #region Directory tree

        private IDictionary<UPath, (IList<UPath>, IList<ArchiveFileInfo>)> CreateDirectoryLookup()
        {
            var result = new Dictionary<UPath, (IList<UPath>, IList<ArchiveFileInfo>)>
            {
                // Add root manually
                [UPath.Root] = (new List<UPath>(), new List<ArchiveFileInfo>())
            };

            foreach (var file in ArchiveState.Files)
            {
                var path = file.FilePath.GetDirectory();
                CreateDirectoryEntries(result, path);

                result[path].Item2.Add(file);
            }

            return result;
        }

        private void CreateDirectoryEntries(UPath newPath)
        {
            CreateDirectoryEntries(_directoryDictionary, newPath);
        }

        private void CreateDirectoryEntries(IDictionary<UPath, (IList<UPath>, IList<ArchiveFileInfo>)> directories, UPath newPath)
        {
            var path = UPath.Root;
            foreach (var part in newPath.Split())
            {
                // Initialize parent entry if not existing
                if (!directories.ContainsKey(path))
                    directories[path] = (new List<UPath>(), new List<ArchiveFileInfo>());

                // Add current directory to parent
                if (!directories[path].Item1.Contains(path / part))
                    directories[path].Item1.Add(path / part);

                path /= part;

                // Initialize current directory if not existing
                if (!directories.ContainsKey(path))
                    directories[path] = (new List<UPath>(), new List<ArchiveFileInfo>());
            }
        }

        #endregion
    }
}

using System;
using System.Diagnostics;
using Raven.Client.Util;
using Sparrow.Json.Parsing;

namespace Raven.Client.Server.PeriodicBackup
{
    public abstract class BackupStatus
    {
        public DateTime? LastFullBackup { get; set; }

        public DateTime? LastIncrementalBackup { get; set; }

        public long? FullBackupDurationInMs { get; set; }

        public long? IncrementalBackupDurationInMs { get; set; }

        public Exception Exception { get; set; }

        public IDisposable UpdateStats(bool isFullBackup, Reference<Exception> exception)
        {
            var now = SystemTime.UtcNow;
            var sw = Stopwatch.StartNew();

            return new DisposableAction(() =>
            {
                if (isFullBackup)
                {
                    LastFullBackup = now;
                    FullBackupDurationInMs = sw.ElapsedMilliseconds;
                }
                else
                {
                    LastIncrementalBackup = now;
                    IncrementalBackupDurationInMs = sw.ElapsedMilliseconds;
                }

                Exception = exception.Value;
            });
        }

        public virtual DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(LastFullBackup)] = LastFullBackup,
                [nameof(LastIncrementalBackup)] = LastIncrementalBackup,
                [nameof(FullBackupDurationInMs)] = FullBackupDurationInMs,
                [nameof(IncrementalBackupDurationInMs)] = IncrementalBackupDurationInMs,
                [nameof(Exception)] = Exception
            };
        }
    }

    public class LocalBackup : BackupStatus
    {
        public string BackupDirectory { get; set; }

        public bool TempFolderUsed { get; set; }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(BackupDirectory)] = BackupDirectory; //TODO: json[BackupDirectory] = BackupDirectory;
            json[nameof(TempFolderUsed)] = TempFolderUsed;
            return json;
        }
    }

    public abstract class CloudUploadStatus : BackupStatus
    {
        protected CloudUploadStatus()
        {
            UploadProgress = new UploadProgress();
        }

        public UploadProgress UploadProgress { get; set; }
    }

    public class UploadToS3 : CloudUploadStatus
    {
        
    }

    public class UploadToGlacier : CloudUploadStatus
    {

    }

    public class UploadToAzure : CloudUploadStatus
    {

    }

    public class UploadProgress
    {
        public UploadProgress()
        {
            UploadType = UploadType.Regular;
        }

        public long Uploaded { get; set; }

        public long Total { get; set; }

        public UploadState UploadState { get; private set; }

        public UploadType UploadType { get; set; }

        public void ChangeState(UploadState newState)
        {
            UploadState = newState;
        }

        public void SetTotal(long totalLength)
        {
            Total = totalLength;
        }

        public void UpdateUploaded(long length)
        {
            Uploaded += length;
        }

        public void ChangeType(UploadType newType)
        {
            UploadType = newType;
        }
    }

    public enum UploadState
    {
        PendingUpload,
        Uploading,
        PendingResponse,
        Done
    }

    public enum UploadType
    {
        Regular,
        Chunked
    }
}
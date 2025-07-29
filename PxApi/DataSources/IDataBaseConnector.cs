using PxApi.Models;

namespace PxApi.DataSources
{
    public interface IDataBaseConnector
    {
        public DataBaseRef DataBase { get; }

        public Task<string[]> GetAllFilesAsync();

        public Stream ReadPxFile(PxFileRef file);

        public Task<DateTime> GetLastWriteTimeAsync(PxFileRef file);
    }
}

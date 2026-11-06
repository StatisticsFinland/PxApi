using Px.Utils.Models.Metadata;
using PxApi.Caching;
using PxApi.Models;

namespace PxApi.UnitTests.Utils
{
    internal static class MetaCacheUtils
    {
        internal static int GetCacheKey(PxFileRef tableId)
        {
            // Use reflection to get the private META_SEED constant from MatrixCache
            System.Reflection.FieldInfo? field = typeof(DatabaseCache).GetField(
                "META_SEED",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?? throw new InvalidOperationException("META_SEED field not found on MatrixCache.");
            string metaSeed = (string)field.GetValue(null)!;

            // Calculate the same hash code that MatrixCache uses
            return HashCode.Combine(metaSeed, tableId);
        }

        internal static object GetCacheKey(PxFileRef file, MetaCacheContainer metaContainer, IMatrixMap map)
        {
            System.Reflection.MethodInfo? method = typeof(DatabaseCache).GetMethod(
                "GetDataCacheKey",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                null,
                [typeof(PxFileRef), typeof(MetaCacheContainer), typeof(IMatrixMap)],
                null);
            return method == null
                ? throw new InvalidOperationException("GetDataCacheKey method not found on DatabaseCache.")
                : method.Invoke(null, [file, metaContainer, map])!;
        }
    }
}

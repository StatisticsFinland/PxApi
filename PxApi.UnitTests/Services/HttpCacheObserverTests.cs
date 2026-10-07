using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using PxApi.Services;

namespace PxApi.UnitTests.Services;

[TestFixture]
public class HttpCacheObserverTests
{
    [Test]
    public void RecordLookups_TrackedRequest_AccumulatesEachCacheCategory()
    {
        DefaultHttpContext context = new();
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        context.Features.Set(observation);
        Mock<IHttpContextAccessor> accessor = new();
        accessor.SetupGet(item => item.HttpContext).Returns(context);
        HttpCacheObserver observer = new(accessor.Object);

        observer.RecordFileListLookup(true);
        observer.RecordFileListLookup(false);
        observer.RecordMetadataLookup(true);
        observer.RecordMetadataLookup(false);
        observer.RecordMetadataLookup(false);
        Dictionary<string, object?> fields = observation.Complete().ToDictionary();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields["file_list_cache_hits"], Is.EqualTo(1L));
            Assert.That(fields["file_list_cache_misses"], Is.EqualTo(1L));
            Assert.That(fields["metadata_cache_hits"], Is.EqualTo(1L));
            Assert.That(fields["metadata_cache_misses"], Is.EqualTo(2L));
        }
    }

    [TestCase("exact_hit")]
    [TestCase("superset_hit")]
    [TestCase("miss")]
    public void RecordData_TrackedRequest_PreservesOutcome(string outcome)
    {
        DefaultHttpContext context = new();
        QueryObservation observation = new(new ConfigurationBuilder().Build());
        context.Features.Set(observation);
        Mock<IHttpContextAccessor> accessor = new();
        accessor.SetupGet(item => item.HttpContext).Returns(context);
        HttpCacheObserver observer = new(accessor.Object);

        if (outcome == "miss") observer.RecordDataCacheMiss();
        else observer.RecordDataCacheHit(outcome == "superset_hit");

        Assert.That(observation.Complete().ToDictionary()["data_cache_outcome"], Is.EqualTo(outcome));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Record_UntrackedContext_DoesNothing(bool hasContext)
    {
        HttpContext? context = hasContext ? new DefaultHttpContext() : null;
        Mock<IHttpContextAccessor> accessor = new();
        accessor.SetupGet(item => item.HttpContext).Returns(context);
        HttpCacheObserver observer = new(accessor.Object);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() =>
            {
                observer.RecordFileListLookup(true);
                observer.RecordMetadataLookup(false);
                observer.RecordDataCacheHit(false);
                observer.RecordDataCacheMiss();
            }, Throws.Nothing);
            if (context is not null) Assert.That(QueryObservation.Get(context), Is.Null);
        }
    }

    [Test]
    public void Record_ContextChanges_UsesCurrentRequestWithoutRetainingPreviousState()
    {
        DefaultHttpContext firstContext = new();
        DefaultHttpContext secondContext = new();
        QueryObservation first = new(new ConfigurationBuilder().Build());
        QueryObservation second = new(new ConfigurationBuilder().Build());
        firstContext.Features.Set(first);
        secondContext.Features.Set(second);
        HttpContext? currentContext = firstContext;
        Mock<IHttpContextAccessor> accessor = new();
        accessor.SetupGet(item => item.HttpContext).Returns(() => currentContext);
        HttpCacheObserver observer = new(accessor.Object);

        observer.RecordDataCacheHit(false);
        currentContext = secondContext;
        observer.RecordDataCacheMiss();
        currentContext = null;
        observer.RecordDataCacheHit(true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Complete().ToDictionary()["data_cache_outcome"], Is.EqualTo("exact_hit"));
            Assert.That(second.Complete().ToDictionary()["data_cache_outcome"], Is.EqualTo("miss"));
        }
    }
}
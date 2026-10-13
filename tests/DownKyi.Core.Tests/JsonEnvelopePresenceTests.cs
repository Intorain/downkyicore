using DownKyi.Core.BiliApi;
using DownKyi.Core.BiliApi.Video.Models;
using Newtonsoft.Json;

namespace DownKyi.Core.Tests;

public sealed class JsonEnvelopePresenceTests
{
    [Fact]
    public void MissingBilibiliDataRemainsNull()
    {
        var response = JsonConvert.DeserializeObject<VideoViewOrigin>("{\"code\":0}");

        Assert.NotNull(response);
        Assert.Null(response.Data);
    }

    [Fact]
    public void RequiredBilibiliPayloadRejectsMissingFieldWithOperationContext()
    {
        var exception = Assert.Throws<BilibiliApiResponseException>(() =>
            BiliApiRequest.RequirePayload<VideoView>(
                null,
                fieldName: "data",
                operationName: "fixture-operation"));

        Assert.Equal("fixture-operation", exception.Operation);
        Assert.Contains("required 'data'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredBilibiliPayloadReturnsExistingObject()
    {
        var payload = new VideoView();

        Assert.Same(
            payload,
            BiliApiRequest.RequirePayload(payload, operationName: "fixture-operation"));
    }
}

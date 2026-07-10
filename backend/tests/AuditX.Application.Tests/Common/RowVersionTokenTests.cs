using AuditX.Application.Common.Concurrency;

namespace AuditX.Application.Tests.Common;

public sealed class RowVersionTokenTests
{
    // A rowversion whose STANDARD base64 encoding contains '+' and '/' (0xFB 0xEF 0xBE 0xFF 0x3F -> "++++/w==" family),
    // i.e. exactly the bytes that used to break a raw ?version= delete.
    private static readonly byte[] ProblematicVersion = [0xFB, 0xEF, 0xBE, 0xFF, 0x3F, 0xFB, 0xFF, 0xBF];

    [Fact]
    public void Encode_is_url_safe_no_plus_slash_or_padding()
    {
        var token = RowVersionToken.Encode(ProblematicVersion);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void Token_survives_query_string_round_trip_without_a_false_conflict()
    {
        var token = RowVersionToken.Encode(ProblematicVersion);
        // ASP.NET Core query parsing decodes '+' to a space; base64url has no '+', so the token is unchanged and the
        // version still matches (the exact 409-forever footgun this fix closes).
        var afterQueryDecode = token.Replace('+', ' ');
        Assert.Equal(token, afterQueryDecode);
        Assert.True(RowVersionToken.Matches(ProblematicVersion, afterQueryDecode));
    }

    [Fact]
    public void Encode_round_trips_and_matches_itself()
    {
        var token = RowVersionToken.Encode(ProblematicVersion);
        Assert.True(RowVersionToken.Matches(ProblematicVersion, token));
        Assert.False(RowVersionToken.Matches(ProblematicVersion, token + "x"));
    }

    [Fact]
    public void Empty_or_null_version_encodes_to_empty()
    {
        Assert.Equal(string.Empty, RowVersionToken.Encode(null));
        Assert.Equal(string.Empty, RowVersionToken.Encode([]));
    }
}

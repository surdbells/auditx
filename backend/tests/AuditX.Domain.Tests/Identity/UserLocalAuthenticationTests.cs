using AuditX.Domain.Enums;
using AuditX.Domain.Identity;

namespace AuditX.Domain.Tests.Identity;

public sealed class UserLocalAuthenticationTests
{
    [Fact]
    public void CreateLocal_produces_a_local_user_with_a_synthetic_object_sid()
    {
        var u = User.CreateLocal("jdoe", "jdoe@example.com", "Jane", "Doe");

        Assert.Equal(AuthenticationSource.Local, u.AuthenticationSource);
        Assert.Equal("jdoe", u.Username);
        Assert.Equal("jdoe", u.AdSamAccountName);
        Assert.StartsWith("local:", u.AdObjectSid);
        Assert.Equal("Jane Doe", u.DisplayName);
        Assert.Equal(UserStatus.AwaitingRoleAssignment, u.Status);
    }

    [Fact]
    public void Directory_users_default_to_directory_source_and_no_username()
    {
        var u = User.ProvisionFromDirectory("jdoe", "jdoe@corp.local", "S-1-5-21", "jdoe@corp.com", "Jane", "Doe");
        Assert.Equal(AuthenticationSource.Directory, u.AuthenticationSource);
        Assert.Null(u.Username);
    }

    [Fact]
    public void EnableLocalAuthentication_switches_a_directory_user_to_local()
    {
        var u = User.ProvisionFromDirectory("jdoe", "jdoe@corp.local", "S-1-5-21", "jdoe@corp.com", "Jane", "Doe");
        u.EnableLocalAuthentication("jane.doe");
        Assert.Equal(AuthenticationSource.Local, u.AuthenticationSource);
        Assert.Equal("jane.doe", u.Username);
    }
}

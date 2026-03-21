using ZeroTrust.Backend.Models;
using ZeroTrust.Backend.Services;
using Xunit;

namespace ZeroTrust.Backend.Tests;

public class CognitoGroupMapperTests
{
    private static List<Organization> BuildOrganizations() =>
    [
        new Organization
        {
            Id = "org-a",
            Name = "Organization A (Data Custodian)",
            ShortName = "ORG-A",
            CognitoIdpName = "IDP-A",
            CognitoGroupName = "ap-southeast-1_abcde_IDP-A"
        },
        new Organization
        {
            Id = "org-b",
            Name = "Organization B (Data Readers)",
            ShortName = "ORG-B",
            CognitoIdpName = "IDP-B",
            CognitoGroupName = "ap-southeast-1_fghij_IDP-B"
        }
    ];

    [Fact]
    public void FindMatchingOrganization_PrefersExactCognitoGroupMatch()
    {
        var organizations = BuildOrganizations();

        var result = CognitoGroupMapper.FindMatchingOrganization(
            organizations,
            ["ap-southeast-1_fghij_IDP-B", "ap-southeast-1_abcde_IDP-A"]);

        Assert.NotNull(result);
        Assert.Equal("ORG-B", result!.ShortName);
    }

    [Fact]
    public void FindMatchingOrganization_FallsBackToIdpNameFromSuffix()
    {
        var organizations = BuildOrganizations();

        var result = CognitoGroupMapper.FindMatchingOrganization(
            organizations,
            ["ap-southeast-1_randomPool_IDP-B"]);

        Assert.NotNull(result);
        Assert.Equal("ORG-B", result!.ShortName);
    }
}

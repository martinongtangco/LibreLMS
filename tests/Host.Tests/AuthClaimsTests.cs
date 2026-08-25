using System.Security.Claims;
using LibreLms.Host.ManagementAuth;
using LibreLms.SharedKernel;

namespace Host.Tests;

/// <summary>
/// Story 040 — the auth cookie's claim contract, pinned to the build.
///
/// bug-039: the OrganizationId claim was silently dropped from the sign-in
/// cookie (duplicated builder lists drifted), and every OrgAdmin dashboard
/// rendered 0/0/0/0 with an empty completion rate until a user reported it.
/// These tests fail the build if any claim type is removed, renamed, or its
/// value reshaped — no browser required. If you change the claim set in
/// AuthClaims, change these tests deliberately in the same commit.
/// </summary>
public class AuthClaimsTests
{
    private static readonly Guid StudentId = Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
    private static readonly Guid OrgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Stamp = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void Build_produces_exactly_the_seven_expected_claim_types_for_a_complete_account()
    {
        var claims = AuthClaims.Build(
            StudentId, "Alice Johnson", "alice@example.com", Stamp, OrgId,
            RoleNames.OrgAdmin, "/avatars/alice.png");

        var byType = claims.ToDictionary(c => c.Type);

        // The contract: exactly these seven types — no more, no fewer.
        var expected = new HashSet<string>
        {
            ClaimTypes.NameIdentifier,
            ClaimTypes.Name,
            ClaimTypes.Email,
            SecurityClaims.SecurityStamp,
            OrgClaimTypes.OrganizationId,
            ClaimTypes.Role,
            AvatarClaimTypes.AvatarPath,
        };
        Assert.Equal(expected, byType.Keys.ToHashSet());
    }

    [Fact]
    public void Build_carrying_correct_values_for_every_claim()
    {
        var claims = AuthClaims.Build(
            StudentId, "Alice Johnson", "alice@example.com", Stamp, OrgId,
            RoleNames.OrgAdmin, "/avatars/alice.png");
        var byType = claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(StudentId.ToString("D"), byType[ClaimTypes.NameIdentifier]);
        Assert.Equal("Alice Johnson", byType[ClaimTypes.Name]);
        Assert.Equal("alice@example.com", byType[ClaimTypes.Email]);
        Assert.Equal(Stamp.ToString("D"), byType[SecurityClaims.SecurityStamp]);
        Assert.Equal(RoleNames.OrgAdmin, byType[ClaimTypes.Role]);
        Assert.Equal("/avatars/alice.png", byType[AvatarClaimTypes.AvatarPath]);
    }

    [Fact]
    public void Build_OrganizationId_claim_parses_back_to_the_accounts_org()
    {
        // bug-039 regression guard: AuthHelpers.GetCurrentUserOrgId requires the
        // claim value to be a parseable Guid equal to the account's org.
        var claims = AuthClaims.Build(
            StudentId, "Admin User", "admin@example.com", Stamp, OrgId,
            RoleNames.OrgAdmin);

        var orgClaim = claims.Single(c => c.Type == OrgClaimTypes.OrganizationId);
        Assert.True(Guid.TryParse(orgClaim.Value, out var orgId));
        Assert.Equal(OrgId, orgId);
    }

    [Fact]
    public void Build_omits_role_and_avatar_claims_when_unset_but_keeps_OrganizationId()
    {
        var claims = AuthClaims.Build(
            StudentId, "Bob Smith", "bob@example.com", Stamp, OrgId);

        var types = claims.Select(c => c.Type).ToHashSet();

        Assert.DoesNotContain(ClaimTypes.Role, types);
        Assert.DoesNotContain(AvatarClaimTypes.AvatarPath, types);
        // The always-present core must survive even for a minimal account.
        Assert.Contains(OrgClaimTypes.OrganizationId, types);
        Assert.Contains(SecurityClaims.SecurityStamp, types);
    }

    [Fact]
    public void Build_omits_role_claim_when_role_is_whitespace_only()
    {
        var claims = AuthClaims.Build(
            StudentId, "Bob Smith", "bob@example.com", Stamp, OrgId, role: "   ");

        Assert.DoesNotContain(claims, c => c.Type == ClaimTypes.Role);
    }
}

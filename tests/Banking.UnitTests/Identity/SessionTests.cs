using Banking.Modules.Identity.Domain;

namespace Banking.UnitTests.Identity;

public sealed class SessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Expiry = Now.AddDays(14);

    [Fact]
    public void A_fresh_token_is_redeemed_once_and_marked_used()
    {
        var (session, token) = NewSession();

        session.Redeem(token, Now).IsSuccess.ShouldBeTrue();

        token.UsedAt.ShouldBe(Now);
        session.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Redeeming_a_used_token_fails_and_revokes_the_session()
    {
        var (session, token) = NewSession();
        session.Redeem(token, Now);

        var reuse = session.Redeem(token, Now.AddMinutes(1));

        reuse.Error.ShouldBe(IdentityErrors.InvalidRefreshToken);
        session.RevokedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void After_a_reuse_the_newest_token_is_dead_too()
    {
        var (session, stolen) = NewSession();
        session.Redeem(stolen, Now);
        var newest = RefreshToken.Issue(session, "hash-2", Expiry);
        session.Redeem(stolen, Now);

        session.Redeem(newest, Now).Error.ShouldBe(IdentityErrors.InvalidRefreshToken);
        newest.UsedAt.ShouldBeNull();
    }

    [Fact]
    public void A_used_token_that_has_also_expired_still_counts_as_reuse()
    {
        var (session, token) = NewSession();
        session.Redeem(token, Now);

        session.Redeem(token, Expiry.AddDays(1));

        session.RevokedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void A_token_works_until_the_moment_it_expires(int secondsAfterExpiry, bool works)
    {
        var (session, token) = NewSession();

        session.Redeem(token, Expiry.AddSeconds(secondsAfterExpiry)).IsSuccess.ShouldBe(works);

        // Waiting too long is not theft.
        session.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void Revoking_twice_keeps_the_first_time()
    {
        var (session, _) = NewSession();

        session.Revoke(Now);
        session.Revoke(Now.AddHours(1));

        session.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public void A_token_of_another_session_is_a_bug()
    {
        var (session, _) = NewSession();
        var (_, foreign) = NewSession();

        Should.Throw<InvalidOperationException>(() => session.Redeem(foreign, Now));
    }

    private static (Session Session, RefreshToken Token) NewSession()
    {
        var session = Session.Start(Guid.CreateVersion7(), Now);
        return (session, RefreshToken.Issue(session, "hash-1", Expiry));
    }
}

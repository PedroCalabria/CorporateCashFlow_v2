using CorporateTreasury.Infrastructure.Auth;

namespace CorporateTreasury.UnitTests.Auth;

public sealed class PasswordHasherAdapterTests
{
    private readonly PasswordHasherAdapter _hasher = new();

    [Fact]
    public void Hash_then_Verify_roundtrips_for_the_correct_password()
    {
        const string password = "S3cure-Password!";

        var hash = _hasher.Hash(password);

        Assert.NotEqual(password, hash); // never stored in plaintext
        Assert.True(_hasher.Verify(hash, password));
    }

    [Fact]
    public void Verify_returns_false_for_a_wrong_password()
    {
        var hash = _hasher.Hash("the-real-password");

        Assert.False(_hasher.Verify(hash, "not-the-password"));
    }
}

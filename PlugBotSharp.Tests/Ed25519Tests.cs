using PlugBotSharp.QQProxy.Crypto;

namespace PlugBotSharp.Tests;

public class Ed25519Tests
{
    private const string Secret1 = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string Public1 = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";
    private const string Signature1 =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private const string Secret2 = "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb";
    private const string Public2 = "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c";
    private const string Signature2 =
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da" +
        "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00";

    private const string Secret3 = "c5aa8df43f9f837bedb7442f31dcb7b166d38535076f094b85ce3a2e0b4458f7";
    private const string Public3 = "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025";
    private const string Signature3 =
        "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac" +
        "18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a";

    [Fact]
    public void PublicKey_matches_rfc8032_test_vector_1()
    {
        Assert.Equal(Public1, Convert.ToHexStringLower(Ed25519.PublicKeyFromSeed(Convert.FromHexString(Secret1))));
    }

    [Fact]
    public void Sign_matches_rfc8032_test_vector_1()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret1), []);

        Assert.Equal(Signature1, Convert.ToHexStringLower(signature));
    }

    [Fact]
    public void Sign_matches_rfc8032_test_vector_2()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret2), Convert.FromHexString("72"));

        Assert.Equal(Signature2, Convert.ToHexStringLower(signature));
    }

    [Fact]
    public void Sign_matches_rfc8032_test_vector_3()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret3), Convert.FromHexString("af82"));

        Assert.Equal(Signature3, Convert.ToHexStringLower(signature));
    }

    [Fact]
    public void Verify_accepts_valid_signature()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret1), "hello"u8);

        Assert.True(Ed25519.Verify(Convert.FromHexString(Public1), "hello"u8, signature));
    }

    [Fact]
    public void Verify_rejects_modified_message()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret1), "hello"u8);

        Assert.False(Ed25519.Verify(Convert.FromHexString(Public1), "hell0"u8, signature));
    }

    [Fact]
    public void Verify_rejects_modified_signature()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret1), "hello"u8);
        signature[0] ^= 0x01;

        Assert.False(Ed25519.Verify(Convert.FromHexString(Public1), "hello"u8, signature));
    }

    [Fact]
    public void Verify_rejects_wrong_public_key()
    {
        var signature = Ed25519.Sign(Convert.FromHexString(Secret1), "hello"u8);

        Assert.False(Ed25519.Verify(Convert.FromHexString(Public2), "hello"u8, signature));
    }

    [Fact]
    public void Verify_rejects_bad_lengths()
    {
        Assert.False(Ed25519.Verify([], "hello"u8, new byte[64]));
        Assert.False(Ed25519.Verify(new byte[32], "hello"u8, new byte[63]));
    }
}

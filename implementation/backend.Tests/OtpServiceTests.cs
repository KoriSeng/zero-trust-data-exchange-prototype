using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;
using ZeroTrust.Backend.Services;
using Xunit;

namespace ZeroTrust.Backend.Tests;

public class OtpServiceTests
{
    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static OtpRecord ValidRecord(string requestId, string code) => new()
    {
        RequestId = requestId,
        RecipientEmail = "owner@example.com",
        CodeHash = HashCode(code),
        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        Used = false,
        AttemptCount = 0
    };

    [Fact]
    public async Task GenerateAndSendOtp_CreatesRecordAndSendsEmail()
    {
        var dataService = Substitute.For<IDataService>();
        var sesService = Substitute.For<ISesService>();
        var logger = Substitute.For<ILogger<OtpService>>();
        var service = new OtpService(dataService, sesService, logger);

        await service.GenerateAndSendOtpAsync("req-001", "owner@example.com");

        await dataService.Received(1).CreateOtpRecordAsync(
            Arg.Is<OtpRecord>(r =>
                r.RequestId == "req-001" &&
                r.RecipientEmail == "owner@example.com" &&
                !string.IsNullOrWhiteSpace(r.CodeHash) &&
                r.CodeHash.Length == 64 &&
                r.ExpiresAt > DateTime.UtcNow));

        await sesService.Received(1).SendOtpEmailAsync(
            "owner@example.com",
            "req-001",
            Arg.Is<string>(c => c.Length == 6 && c.All(char.IsDigit)));
    }

    [Fact]
    public async Task ValidateOtp_CorrectCode_ReturnsSuccessAndMarksUsed()
    {
        const string requestId = "req-002";
        const string code = "482931";
        var record = ValidRecord(requestId, code);

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);
        dataService.TryMarkOtpUsedAsync(record.Id, record.AttemptCount).Returns(true);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, code);

        Assert.Equal(OtpValidationResult.Success, result);
        await dataService.Received(1).TryMarkOtpUsedAsync(record.Id, record.AttemptCount);
    }

    [Fact]
    public async Task ValidateOtp_ExpiredRecord_ReturnsExpired()
    {
        const string requestId = "req-003";
        const string code = "111111";
        var record = ValidRecord(requestId, code);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, code);

        Assert.Equal(OtpValidationResult.Expired, result);
        Assert.False(record.Used);
    }

    [Fact]
    public async Task ValidateOtp_AttemptCountAtMax_ReturnsMaxAttemptsExceeded()
    {
        const string requestId = "req-004";
        const string code = "222222";
        var record = ValidRecord(requestId, code);
        record.AttemptCount = 3;

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, "999999");

        Assert.Equal(OtpValidationResult.MaxAttemptsExceeded, result);
        Assert.Equal(3, record.AttemptCount);
    }

    [Fact]
    public async Task ValidateOtp_AlreadyUsedRecord_ReturnsAlreadyUsed()
    {
        const string requestId = "req-005";
        const string code = "333333";
        var record = ValidRecord(requestId, code);
        record.Used = true;

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, code);

        Assert.Equal(OtpValidationResult.AlreadyUsed, result);
    }

    [Fact]
    public async Task ValidateOtp_NoRecord_ReturnsNotFound()
    {
        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync("req-999").Returns((OtpRecord?)null);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync("req-999", "000000");

        Assert.Equal(OtpValidationResult.NotFound, result);
    }

    [Fact]
    public async Task ValidateOtp_WrongCode_IncrementsAttemptCount()
    {
        const string requestId = "req-006";
        const string code = "444444";
        var record = ValidRecord(requestId, code);

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);
        dataService.TryIncrementOtpAttemptAsync(record.Id, record.AttemptCount, Arg.Any<DateTime?>()).Returns(true);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, "000000");

        Assert.Equal(OtpValidationResult.InvalidCode, result);
        await dataService.Received(1).TryIncrementOtpAttemptAsync(
            record.Id,
            record.AttemptCount,
            Arg.Is<DateTime?>(value => !value.HasValue));
    }

    [Fact]
    public async Task ValidateOtp_ThirdWrongCode_LocksRecordAndReturnsMaxAttemptsExceeded()
    {
        const string requestId = "req-007";
        const string code = "555555";
        var record = ValidRecord(requestId, code);
        record.AttemptCount = 2;

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(record);
        dataService.TryIncrementOtpAttemptAsync(record.Id, record.AttemptCount, Arg.Any<DateTime?>()).Returns(true);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, "000000");

        Assert.Equal(OtpValidationResult.MaxAttemptsExceeded, result);
        await dataService.Received(1).TryIncrementOtpAttemptAsync(
            record.Id,
            record.AttemptCount,
            Arg.Is<DateTime?>(value => value.HasValue));
    }

    [Fact]
    public async Task ValidateOtp_WhenConcurrentValidationUsesCodeSecondCallReturnsAlreadyUsed()
    {
        const string requestId = "req-008";
        const string code = "666666";

        var firstRead = ValidRecord(requestId, code);
        var secondRead = ValidRecord(requestId, code);
        secondRead.Id = firstRead.Id;
        secondRead.Used = true;

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(firstRead, secondRead);
        dataService.TryMarkOtpUsedAsync(firstRead.Id, firstRead.AttemptCount).Returns(false);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, code);

        Assert.Equal(OtpValidationResult.AlreadyUsed, result);
        await dataService.Received(1).TryMarkOtpUsedAsync(firstRead.Id, firstRead.AttemptCount);
    }

    [Fact]
    public async Task ValidateOtp_WrongCodeCompareAndSetConflictRetriesAndReturnsInvalidCode()
    {
        const string requestId = "req-009";
        const string code = "777777";

        var firstRead = ValidRecord(requestId, code);
        var secondRead = ValidRecord(requestId, code);
        secondRead.Id = firstRead.Id;
        secondRead.AttemptCount = 1;

        var dataService = Substitute.For<IDataService>();
        dataService.GetLatestOtpRecordAsync(requestId).Returns(firstRead, secondRead);
        dataService.TryIncrementOtpAttemptAsync(firstRead.Id, 0, Arg.Any<DateTime?>()).Returns(false);
        dataService.TryIncrementOtpAttemptAsync(firstRead.Id, 1, Arg.Any<DateTime?>()).Returns(true);

        var service = new OtpService(
            dataService,
            Substitute.For<ISesService>(),
            Substitute.For<ILogger<OtpService>>());

        var result = await service.ValidateOtpAsync(requestId, "000000");

        Assert.Equal(OtpValidationResult.InvalidCode, result);
        await dataService.Received(1).TryIncrementOtpAttemptAsync(
            firstRead.Id,
            0,
            Arg.Any<DateTime?>());
        await dataService.Received(1).TryIncrementOtpAttemptAsync(
            firstRead.Id,
            1,
            Arg.Any<DateTime?>());
    }
}

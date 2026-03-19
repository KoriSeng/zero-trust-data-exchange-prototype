using MongoDB.Driver;
using ZeroTrust.Backend.Data;
using ZeroTrust.Backend.Models;

namespace ZeroTrust.Backend.Services;

/// <summary>
/// Hosted service that seeds the database with initial data on application startup.
/// Runs in the background and ensures the database is initialized before the app starts handling requests.
/// </summary>
public class DatabaseSeederHostedService : IHostedService
{
    private readonly IDataService _dataService;
    private readonly ILogger<DatabaseSeederHostedService> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly IConfiguration _configuration;

    public DatabaseSeederHostedService(
        IDataService dataService,
        ILogger<DatabaseSeederHostedService> logger,
        IMongoClient mongoClient,
        IConfiguration configuration)
    {
        _dataService = dataService;
        _mongoClient = mongoClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database seeder starting...");

        try
        {                     
            _mongoClient.DropDatabaseAsync("zero_trust_db").Wait(cancellationToken);
            _logger.LogInformation("Dropped existing database for fresh seeding.");
            await SeedDatabaseAsync(cancellationToken);
            _logger.LogInformation("Database seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database seeding");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database seeder stopping...");
        return Task.CompletedTask;
    }

    private async Task SeedDatabaseAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database seed initialization...");

        // Create roles
        _logger.LogInformation("Creating roles...");
        var roles = new[]
        {
            new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Requester, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.DataOwner, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Admin, CreatedAt = DateTime.UtcNow },
            new Role { Id = Guid.NewGuid().ToString(), Name = RoleNames.Auditor, CreatedAt = DateTime.UtcNow }
        };
        foreach (var role in roles)
        {
            await _dataService.PutRoleAsync(role);
        }
        _logger.LogInformation("✓ Created 4 roles");

        // Create organizations
        _logger.LogInformation("Creating organizations...");
        var orgAId = Guid.NewGuid().ToString();
        var orgBId = Guid.NewGuid().ToString();
        var orgAGroupName = _configuration["SeedData:OrgA:CognitoGroupName"] ?? "IDP-A";
        var orgBGroupName = _configuration["SeedData:OrgB:CognitoGroupName"] ?? "IDP-B";
        var orgAIssuer = _configuration["SeedData:OrgA:IdpIssuer"] ?? "http://localhost:9001";
        var orgBIssuer = _configuration["SeedData:OrgB:IdpIssuer"] ?? "http://localhost:9002";

        var organizations = new[]
        {
            new Organization
            {
                Id = orgAId,
                Name = "Organization A (Data Custodian)",
                ShortName = "ORG-A",
                CognitoIdpName = "IDP-A",
                CognitoGroupName = orgAGroupName,
                IdpIssuer = orgAIssuer,
                ContactEmail = "a.approvals@org-a.example.com",
                Status = OrganizationStatus.Active,
                OnboardedAt = DateTime.UtcNow
            },
            new Organization
            {
                Id = orgBId,
                Name = "Organization B (Data Readers)",
                ShortName = "ORG-B",
                CognitoIdpName = "IDP-B",
                CognitoGroupName = orgBGroupName,
                IdpIssuer = orgBIssuer,
                ContactEmail = "b.approvals@org-b.example.com",
                Status = OrganizationStatus.Active,
                OnboardedAt = DateTime.UtcNow
            }
        };
        foreach (var org in organizations)
        {
            await _dataService.PutOrganizationAsync(org);
        }
        _logger.LogInformation("✓ Created 2 organizations");

        // Get role IDs for user assignment
        var requesterRole = await _dataService.GetRoleByNameAsync(RoleNames.Requester);
        var dataOwnerRole = await _dataService.GetRoleByNameAsync(RoleNames.DataOwner);
        var adminRole = await _dataService.GetRoleByNameAsync(RoleNames.Admin);
        var auditorRole = await _dataService.GetRoleByNameAsync(RoleNames.Auditor);

        // Create users
        _logger.LogInformation("Creating test users with FIXED SUBJECTS...");
        var users = new[]
        {
            // IDP-A Requesters
            (user: new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = "IDP-A_A-001",
                CognitoUsername = "IDP-A_a.alex",
                FederatedSub = "A-001",
                IdentityProvider = "IDP-A",
                Issuer = orgAIssuer,
                Email = "a.alex@org-a.example.com",
                DisplayName = "Alex Kim",
                OrganizationId = orgAId,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            }, roleId: requesterRole?.Id),

            (user: new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = "IDP-A_A-002",
                CognitoUsername = "IDP-A_a.sam",
                FederatedSub = "A-002",
                IdentityProvider = "IDP-A",
                Issuer = orgAIssuer,
                Email = "a.sam@org-a.example.com",
                DisplayName = "Sam Lee",
                OrganizationId = orgAId,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            }, roleId: requesterRole?.Id),

            // IDP-B Data Owners
            (user: new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = "IDP-B_B-001",
                CognitoUsername = "IDP-B_b.alex",
                FederatedSub = "B-001",
                IdentityProvider = "IDP-B",
                Issuer = orgBIssuer,
                Email = "b.alex@org-b.example.com",
                DisplayName = "Alex Kim", // Collision test: same name as IDP-A user
                OrganizationId = orgBId,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            }, roleId: dataOwnerRole?.Id),

            (user: new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = "IDP-B_B-002",
                CognitoUsername = "IDP-B_b.jamie",
                FederatedSub = "B-002",
                IdentityProvider = "IDP-B",
                Issuer = orgBIssuer,
                Email = "b.jamie@org-b.example.com",
                DisplayName = "Jamie Tan",
                OrganizationId = orgBId,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            }, roleId: dataOwnerRole?.Id),

            // Admin
            (user: new User
            {
                Id = Guid.NewGuid().ToString(),
                Sub = "IDP-A_ADMIN-001",
                CognitoUsername = "IDP-A_admin",
                FederatedSub = "ADMIN-001",
                IdentityProvider = "IDP-A",
                Issuer = orgAIssuer,
                Email = "admin@org-custodian.example.com",
                DisplayName = "System Administrator",
                OrganizationId = orgAId,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow,
                LastAccessAt = DateTime.UtcNow
            }, roleId: adminRole?.Id)
        };

        foreach (var (user, roleId) in users)
        {
            await _dataService.PutUserAsync(user);
            if (roleId != null)
            {
                await _dataService.AssignRoleToUserAsync(user.Id, roleId, "system");
            }
        }

        // Admin gets auditor role too
        if (adminRole?.Id != null && auditorRole?.Id != null)
        {
            await _dataService.AssignRoleToUserAsync(users[4].user.Id, auditorRole.Id, "system");
        }
        _logger.LogInformation("✓ Created 5 users with assigned roles");

        // Create user agreement
        _logger.LogInformation("Creating user agreements...");
        var agreement = new UserAgreement
        {
            AgreementId = "AGREEMENT-001",
            Version = 1,
            Name = "Data Access Terms & Conditions",
            Content = @"# Data Access Agreement

## 1. Purpose
This agreement governs access to sensitive datasets managed by the Data Custodian.

## 2. Terms
- Data shall be used solely for approved research purposes
- Data shall not be shared with unauthorized parties
- Data shall be deleted after the access period expires
- Any breach shall be reported immediately

## 3. Compliance
Requester agrees to comply with all applicable data protection regulations.

## 4. Audit and Monitoring
All access is logged and subject to audit.

Effective Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd"),
            EffectiveDate = DateTime.UtcNow,
            Status = AgreementStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
        await _dataService.PutAgreementAsync(agreement);
        _logger.LogInformation("✓ Created 1 user agreement");

        // Create sample request
        _logger.LogInformation("Creating sample requests...");
        var alexKimUser = users[0].user; // IDP-A_A-001
        var request = new DataAccessRequest
        {
            Id = Guid.NewGuid().ToString(),
            RequestId = "REQ-2024-001",
            RequesterId = alexKimUser.Id,  // UUID — matches user_id claim from JIT provisioning
            RequesterEmail = "a.alex@org-a.example.com",
            RequesterOrg = orgAId,
            DataOwnerOrg = orgBId,
            DatasetId = "dataset-genomic-2024",
            DatasetName = "Genomic Research Dataset 2024",
            ObjectKeys = new List<string> { "samples/data-001.csv", "samples/data-002.csv" },
            Purpose = "Machine learning model training",
            Status = RequestStatus.PendingOwnerApproval,
            UserAgreementId = "AGREEMENT-001",
            UserAgreementVersion = 1,
            AgreementContent = agreement.Content,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(5)
        };
        await _dataService.PutRequestAsync(request);

        // REQ-2024-002 — seeded for denial-flow testing in k6 (requester: Sam, IDP-A_A-002)
        var samLeeUser = users[1].user; // IDP-A_A-002
        var denyRequest = new DataAccessRequest
        {
            Id = Guid.NewGuid().ToString(),
            RequestId = "REQ-2024-002",
            RequesterId = samLeeUser.Id,  // UUID — matches user_id claim from JIT provisioning
            RequesterEmail = "a.sam@org-a.example.com",
            RequesterOrg = orgAId,
            DataOwnerOrg = orgBId,
            DatasetId = "dataset-proteomics-2024",
            DatasetName = "Proteomics Research Dataset 2024",
            ObjectKeys = new List<string> { "samples/data-003.csv" },
            Purpose = "Proteomics secondary analysis",
            Status = RequestStatus.PendingOwnerApproval,
            UserAgreementId = "AGREEMENT-001",
            UserAgreementVersion = 1,
            AgreementContent = agreement.Content,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(6)
        };
        await _dataService.PutRequestAsync(denyRequest);
        _logger.LogInformation("✓ Created 2 sample requests");

        _logger.LogInformation("Creating dataset catalog items...");
        var datasetCatalogItems = new[]
        {
            new DatasetCatalogItem
            {
                Id = Guid.NewGuid().ToString(),
                DatasetId = "dataset-genomic-2024",
                Name = "Genomic Research Dataset 2024",
                Summary = "Whole-genome variant calls with phenotypic labels for 12,500 synthetic cohorts.",
                DataOwnerOrg = orgBId,
                DataOwnerOrgShortName = "ORG-B",
                DataOwnerOrgName = "Organization B (Data Readers)",
                ObjectKeys = new List<string>
                {
                    "datasets/genomic-2024/variants-part-001.parquet",
                    "datasets/genomic-2024/variants-part-002.parquet",
                    "datasets/genomic-2024/sample-manifest.csv"
                },
                ThumbnailObjectKey = "thumbnails/genomic-2024.png",
                Tags = new List<string> { "genomics", "variant-calls", "parquet" },
                RecordCount = 12500,
                LastUpdatedAt = DateTime.UtcNow.AddDays(-4),
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                IsPublished = true
            },
            new DatasetCatalogItem
            {
                Id = Guid.NewGuid().ToString(),
                DatasetId = "dataset-proteomics-2024",
                Name = "Proteomics Research Dataset 2024",
                Summary = "Synthetic protein abundance matrices for therapy-response benchmarking.",
                DataOwnerOrg = orgBId,
                DataOwnerOrgShortName = "ORG-B",
                DataOwnerOrgName = "Organization B (Data Readers)",
                ObjectKeys = new List<string>
                {
                    "datasets/proteomics-2024/abundance-matrix.tsv",
                    "datasets/proteomics-2024/feature-dictionary.json"
                },
                ThumbnailObjectKey = "thumbnails/proteomics-2024.png",
                Tags = new List<string> { "proteomics", "biomarkers", "tsv" },
                RecordCount = 8600,
                LastUpdatedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-16),
                IsPublished = true
            },
            new DatasetCatalogItem
            {
                Id = Guid.NewGuid().ToString(),
                DatasetId = "dataset-imaging-ct-2025",
                Name = "CT Imaging Cohort 2025",
                Summary = "Anonymized synthetic thoracic CT slices with derived segmentation masks.",
                DataOwnerOrg = orgBId,
                DataOwnerOrgShortName = "ORG-B",
                DataOwnerOrgName = "Organization B (Data Readers)",
                ObjectKeys = new List<string>
                {
                    "datasets/imaging-ct-2025/ct-series-index.csv",
                    "datasets/imaging-ct-2025/segmentation-labels.jsonl"
                },
                ThumbnailObjectKey = "thumbnails/imaging-ct-2025.png",
                Tags = new List<string> { "imaging", "ct", "segmentation" },
                RecordCount = 4200,
                LastUpdatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsPublished = true
            }
        };

        foreach (var dataset in datasetCatalogItems)
        {
            await _dataService.PutDatasetCatalogItemAsync(dataset);
        }
        _logger.LogInformation("✓ Created {count} dataset catalog items", datasetCatalogItems.Length);

        _logger.LogInformation("\n========================================");
        _logger.LogInformation("Database initialized successfully!");
        _logger.LogInformation("========================================");
        _logger.LogInformation("Fixed Test Users for k6:");
        _logger.LogInformation("  IDP-A: IDP-A_A-001 (a.alex), IDP-A_A-002 (a.sam)");
        _logger.LogInformation("  IDP-B: IDP-B_B-001 (b.alex), IDP-B_B-002 (b.jamie)");
        _logger.LogInformation("  Admin: IDP-A_ADMIN-001 (admin)");
        _logger.LogInformation("Collision Test: Both IDP-A_A-001 and IDP-B_B-001 are 'Alex Kim'");
        _logger.LogInformation("========================================\n");
    }
}

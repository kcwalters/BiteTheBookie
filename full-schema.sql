IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'00000000000000_CreateIdentitySchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'00000000000000_CreateIdentitySchema', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251231190214_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251231190214_InitialCreate', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [FirstName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [LastName] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SubscriptionExpiry] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SubscriptionTier] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260328143250_AddSubscriptionFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260328143250_AddSubscriptionFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260329235744_AddExpertPicks'
)
BEGIN
    CREATE TABLE [ExpertPicks] (
        [Id] int NOT NULL IDENTITY,
        [GameId] nvarchar(450) NOT NULL,
        [League] nvarchar(450) NOT NULL,
        [AwayTeamName] nvarchar(max) NOT NULL,
        [HomeTeamName] nvarchar(max) NOT NULL,
        [GameTime] datetime2 NOT NULL,
        [PickType] nvarchar(max) NOT NULL,
        [PickSelection] nvarchar(max) NOT NULL,
        [Confidence] int NOT NULL,
        [Analysis] nvarchar(max) NULL,
        [EnteredBy] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ExpertPicks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260329235744_AddExpertPicks'
)
BEGIN
    CREATE INDEX [IX_ExpertPicks_GameId] ON [ExpertPicks] ([GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260329235744_AddExpertPicks'
)
BEGIN
    CREATE INDEX [IX_ExpertPicks_League] ON [ExpertPicks] ([League]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260329235744_AddExpertPicks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260329235744_AddExpertPicks', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406224143_AddGameSimulations'
)
BEGIN
    CREATE TABLE [GameSimulations] (
        [Id] int NOT NULL IDENTITY,
        [GameId] nvarchar(450) NOT NULL,
        [League] nvarchar(max) NOT NULL,
        [AwayTeamName] nvarchar(max) NOT NULL,
        [HomeTeamName] nvarchar(max) NOT NULL,
        [GameDate] datetime2 NOT NULL,
        [SimulationContent] nvarchar(max) NOT NULL,
        [GeneratedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [GeneratedByUserId] nvarchar(max) NULL,
        CONSTRAINT [PK_GameSimulations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406224143_AddGameSimulations'
)
BEGIN
    CREATE INDEX [IX_GameSimulations_GameId] ON [GameSimulations] ([GameId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406224143_AddGameSimulations'
)
BEGIN
    CREATE INDEX [IX_GameSimulations_GeneratedAt] ON [GameSimulations] ([GeneratedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406224143_AddGameSimulations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406224143_AddGameSimulations', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616201016_AddDataProtectionKeys'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260616201016_AddDataProtectionKeys'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260616201016_AddDataProtectionKeys', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [City] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [DateOfBirth] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [State] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [StreetAddress] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [ZipCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802144100_ExpandUserProfileFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802144100_ExpandUserProfileFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811230700_AddSiteVideos'
)
BEGIN
    CREATE TABLE [SiteVideos] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(1000) NULL,
        [YouTubeUrl] nvarchar(500) NOT NULL,
        [YouTubeId] nvarchar(20) NOT NULL,
        [Category] nvarchar(50) NULL,
        [IsPublished] bit NOT NULL,
        [IsFeatured] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [EnteredBy] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_SiteVideos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811230700_AddSiteVideos'
)
BEGIN
    CREATE INDEX [IX_SiteVideos_IsFeatured] ON [SiteVideos] ([IsFeatured]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811230700_AddSiteVideos'
)
BEGIN
    CREATE INDEX [IX_SiteVideos_IsPublished] ON [SiteVideos] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811230700_AddSiteVideos'
)
BEGIN
    CREATE INDEX [IX_SiteVideos_SortOrder] ON [SiteVideos] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811230700_AddSiteVideos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811230700_AddSiteVideos', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812231257_AddExpertPosts'
)
BEGIN
    CREATE TABLE [ExpertPosts] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(300) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Summary] nvarchar(200) NULL,
        [Category] nvarchar(200) NULL,
        [AuthorId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt] datetime2 NULL,
        [IsPublished] bit NOT NULL,
        [ViewCount] int NOT NULL,
        CONSTRAINT [PK_ExpertPosts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpertPosts_AspNetUsers_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812231257_AddExpertPosts'
)
BEGIN
    CREATE INDEX [IX_ExpertPosts_AuthorId] ON [ExpertPosts] ([AuthorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812231257_AddExpertPosts'
)
BEGIN
    CREATE INDEX [IX_ExpertPosts_IsPublished] ON [ExpertPosts] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812231257_AddExpertPosts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812231257_AddExpertPosts', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE TABLE [FantasyContests] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [League] nvarchar(450) NOT NULL,
        [SlateKey] nvarchar(450) NOT NULL,
        [SalaryCap] int NOT NULL,
        [LockTime] datetime2 NOT NULL,
        [IsScored] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_FantasyContests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE TABLE [FantasyEntries] (
        [Id] int NOT NULL IDENTITY,
        [FantasyContestId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [SubmittedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [TotalSalary] int NOT NULL,
        [TotalPoints] decimal(8,2) NULL,
        CONSTRAINT [PK_FantasyEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FantasyEntries_FantasyContests_FantasyContestId] FOREIGN KEY ([FantasyContestId]) REFERENCES [FantasyContests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE TABLE [FantasyPlayers] (
        [Id] int NOT NULL IDENTITY,
        [FantasyContestId] int NOT NULL,
        [PlayerId] nvarchar(max) NOT NULL,
        [PlayerName] nvarchar(max) NOT NULL,
        [Position] nvarchar(450) NOT NULL,
        [TeamCode] nvarchar(max) NOT NULL,
        [TeamName] nvarchar(max) NOT NULL,
        [OpponentCode] nvarchar(max) NOT NULL,
        [GameId] nvarchar(max) NOT NULL,
        [GameTime] datetime2 NOT NULL,
        [Salary] int NOT NULL,
        [FantasyPoints] decimal(8,2) NULL,
        [ImageUrl] nvarchar(max) NULL,
        CONSTRAINT [PK_FantasyPlayers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FantasyPlayers_FantasyContests_FantasyContestId] FOREIGN KEY ([FantasyContestId]) REFERENCES [FantasyContests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE TABLE [FantasyEntrySlots] (
        [Id] int NOT NULL IDENTITY,
        [FantasyEntryId] int NOT NULL,
        [SlotLabel] nvarchar(max) NOT NULL,
        [FantasyPlayerId] int NOT NULL,
        CONSTRAINT [PK_FantasyEntrySlots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FantasyEntrySlots_FantasyEntries_FantasyEntryId] FOREIGN KEY ([FantasyEntryId]) REFERENCES [FantasyEntries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FantasyEntrySlots_FantasyPlayers_FantasyPlayerId] FOREIGN KEY ([FantasyPlayerId]) REFERENCES [FantasyPlayers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyContests_League] ON [FantasyContests] ([League]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyContests_SlateKey] ON [FantasyContests] ([SlateKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyEntries_FantasyContestId_UserId] ON [FantasyEntries] ([FantasyContestId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyEntrySlots_FantasyEntryId] ON [FantasyEntrySlots] ([FantasyEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyEntrySlots_FantasyPlayerId] ON [FantasyEntrySlots] ([FantasyPlayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyPlayers_FantasyContestId] ON [FantasyPlayers] ([FantasyContestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    CREATE INDEX [IX_FantasyPlayers_Position] ON [FantasyPlayers] ([Position]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260903195123_AddFantasyFootball'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260903195123_AddFantasyFootball', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920185454_AddPayPalSubscriptionFields'
)
BEGIN

                    IF COL_LENGTH('AspNetUsers', 'PayPalSubscriptionId') IS NULL
                        ALTER TABLE [AspNetUsers] ADD [PayPalSubscriptionId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920185454_AddPayPalSubscriptionFields'
)
BEGIN

                    IF COL_LENGTH('AspNetUsers', 'SubscriptionCancelled') IS NULL
                        ALTER TABLE [AspNetUsers] ADD [SubscriptionCancelled] bit NOT NULL CONSTRAINT [DF_AspNetUsers_SubscriptionCancelled] DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260920185454_AddPayPalSubscriptionFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260920185454_AddPayPalSubscriptionFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020184616_AddContentBlocks'
)
BEGIN
    CREATE TABLE [ContentBlocks] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [PageKey] nvarchar(100) NOT NULL,
        [RegionKey] nvarchar(100) NOT NULL,
        [Sport] nvarchar(50) NULL,
        [Team] nvarchar(100) NULL,
        [HtmlContent] nvarchar(max) NOT NULL,
        [IsPublished] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [EnteredBy] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_ContentBlocks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020184616_AddContentBlocks'
)
BEGIN
    CREATE INDEX [IX_ContentBlocks_IsPublished] ON [ContentBlocks] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020184616_AddContentBlocks'
)
BEGIN
    CREATE INDEX [IX_ContentBlocks_PageKey_RegionKey] ON [ContentBlocks] ([PageKey], [RegionKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020184616_AddContentBlocks'
)
BEGIN
    CREATE INDEX [IX_ContentBlocks_SortOrder] ON [ContentBlocks] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020184616_AddContentBlocks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020184616_AddContentBlocks', N'10.0.9');
END;

COMMIT;
GO


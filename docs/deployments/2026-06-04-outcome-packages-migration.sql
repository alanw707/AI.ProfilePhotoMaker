BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE TABLE [OutcomePackageDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [Currency] nvarchar(max) NOT NULL,
        [StripePriceId] nvarchar(max) NULL,
        [InternalCreditPackageId] int NULL,
        [IncludedCandidateCount] int NOT NULL,
        [IncludedRefinementCount] int NOT NULL,
        [IncludedPremiumAugmentationCount] int NOT NULL,
        [IncludesPlatformExportKit] bit NOT NULL,
        [IncludesScoreDelta] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_OutcomePackageDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OutcomePackageDefinitions_CreditPackages_InternalCreditPackageId] FOREIGN KEY ([InternalCreditPackageId]) REFERENCES [CreditPackages] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE TABLE [UserPackageEntitlements] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [OutcomePackageDefinitionId] int NOT NULL,
        [SourcePaymentTransactionId] int NULL,
        [Status] int NOT NULL,
        [RemainingPackageUses] int NOT NULL,
        [RemainingCandidates] int NOT NULL,
        [RemainingRefinements] int NOT NULL,
        [RemainingPremiumAugmentations] int NOT NULL,
        [PlatformExportKitAvailable] bit NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [ConsumedAt] datetime2 NULL,
        [ExpiresAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserPackageEntitlements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserPackageEntitlements_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserPackageEntitlements_OutcomePackageDefinitions_OutcomePackageDefinitionId] FOREIGN KEY ([OutcomePackageDefinitionId]) REFERENCES [OutcomePackageDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserPackageEntitlements_PaymentTransactions_SourcePaymentTransactionId] FOREIGN KEY ([SourcePaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'Currency', N'Description', N'DisplayOrder', N'IncludedCandidateCount', N'IncludedPremiumAugmentationCount', N'IncludedRefinementCount', N'IncludesPlatformExportKit', N'IncludesScoreDelta', N'InternalCreditPackageId', N'IsActive', N'Name', N'Price', N'StripePriceId', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[OutcomePackageDefinitions]'))
        SET IDENTITY_INSERT [OutcomePackageDefinitions] ON;
    EXEC(N'INSERT INTO [OutcomePackageDefinitions] ([Id], [Code], [CreatedAt], [Currency], [Description], [DisplayOrder], [IncludedCandidateCount], [IncludedPremiumAugmentationCount], [IncludedRefinementCount], [IncludesPlatformExportKit], [IncludesScoreDelta], [InternalCreditPackageId], [IsActive], [Name], [Price], [StripePriceId], [UpdatedAt])
    VALUES (1, N''free_preview'', ''2024-01-01T00:00:00.0000000Z'', N''USD'', N''Score your source photo and try a same-quality watermarked preview before buying a package.'', 1, 1, 0, 0, CAST(0 AS bit), CAST(0 AS bit), NULL, CAST(1 AS bit), N''Free Preview'', 0.0, NULL, NULL),
    (2, N''starter_package'', ''2024-01-01T00:00:00.0000000Z'', N''USD'', N''Three profile-photo candidates, best shot selector, basic adjustment, and selected platform exports.'', 2, 3, 0, 2, CAST(1 AS bit), CAST(0 AS bit), 1, CAST(1 AS bit), N''Starter Package'', 9.99, NULL, NULL),
    (3, N''pro_package'', ''2024-01-01T00:00:00.0000000Z'', N''USD'', N''Nine candidates, best shot selector, score delta, exports, refinements, and premium augmentations.'', 3, 9, 3, 5, CAST(1 AS bit), CAST(1 AS bit), 2, CAST(1 AS bit), N''Pro Package'', 19.99, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAt', N'Currency', N'Description', N'DisplayOrder', N'IncludedCandidateCount', N'IncludedPremiumAugmentationCount', N'IncludedRefinementCount', N'IncludesPlatformExportKit', N'IncludesScoreDelta', N'InternalCreditPackageId', N'IsActive', N'Name', N'Price', N'StripePriceId', N'UpdatedAt') AND [object_id] = OBJECT_ID(N'[OutcomePackageDefinitions]'))
        SET IDENTITY_INSERT [OutcomePackageDefinitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OutcomePackageDefinitions_Code_Unique] ON [OutcomePackageDefinitions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE INDEX [IX_OutcomePackageDefinitions_InternalCreditPackageId] ON [OutcomePackageDefinitions] ([InternalCreditPackageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE INDEX [IX_OutcomePackageDefinitions_IsActive_DisplayOrder] ON [OutcomePackageDefinitions] ([IsActive], [DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE INDEX [IX_UserPackageEntitlements_OutcomePackageDefinitionId] ON [UserPackageEntitlements] ([OutcomePackageDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserPackageEntitlements_SourcePaymentTransactionId_Unique] ON [UserPackageEntitlements] ([SourcePaymentTransactionId]) WHERE [SourcePaymentTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    CREATE INDEX [IX_UserPackageEntitlements_User_Status_CreatedAt] ON [UserPackageEntitlements] ([UserId], [Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518040142_AddOutcomePackages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518040142_AddOutcomePackages', N'10.0.5');
END;

COMMIT;
GO


-- EMERGENCY SCRIPT: Create CreditPackages table and seed data
-- Run this if auto-migration continues to fail

-- Check if table already exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CreditPackages')
BEGIN
    PRINT 'Creating CreditPackages table...'
    
    -- Create CreditPackages table
    CREATE TABLE [CreditPackages] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Credits] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [BonusCredits] int NOT NULL,
        [StripeProductId] nvarchar(max) NULL,
        [StripePriceId] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CreditPackages] PRIMARY KEY ([Id])
    );
    
    -- Create unique index on Name
    CREATE UNIQUE INDEX [IX_CreditPackages_Name] ON [CreditPackages] ([Name]);
    
    PRINT 'CreditPackages table created successfully.'
END
ELSE
BEGIN
    PRINT 'CreditPackages table already exists.'
END

-- Check if CreditPurchases table exists, create if needed
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CreditPurchases')
BEGIN
    PRINT 'Creating CreditPurchases table...'
    
    CREATE TABLE [CreditPurchases] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [PackageId] int NOT NULL,
        [PurchaseDate] datetime2 NOT NULL,
        [CreditsAwarded] int NOT NULL,
        [AmountPaid] decimal(10,2) NOT NULL,
        [PaymentTransactionId] nvarchar(100) NULL,
        [PaymentProvider] nvarchar(50) NOT NULL,
        [ExternalTransactionId] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_CreditPurchases] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditPurchases_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CreditPurchases_CreditPackages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [CreditPackages] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_CreditPurchases_PackageId] ON [CreditPurchases] ([PackageId]);
    CREATE INDEX [IX_CreditPurchases_UserId] ON [CreditPurchases] ([UserId]);
    
    PRINT 'CreditPurchases table created successfully.'
END
ELSE
BEGIN
    PRINT 'CreditPurchases table already exists.'
END

-- Seed credit packages data (if not already present)
IF NOT EXISTS (SELECT 1 FROM [CreditPackages])
BEGIN
    PRINT 'Seeding CreditPackages data...'
    
    INSERT INTO [CreditPackages] ([Name], [Credits], [Price], [Description], [IsActive], [DisplayOrder], [BonusCredits], [StripeProductId], [StripePriceId], [CreatedAt], [UpdatedAt])
    VALUES 
        ('Starter Pack', 50, 9.99, 'Perfect for trying out custom training and styled generations', 1, 1, 0, NULL, NULL, GETUTCDATE(), NULL),
        ('Professional Pack', 120, 19.99, 'Most popular - great for professionals', 2, 2, 30, NULL, NULL, GETUTCDATE(), NULL),
        ('Studio Pack', 300, 39.99, 'Best value for content creators and businesses', 1, 3, 100, NULL, NULL, GETUTCDATE(), NULL);
    
    PRINT 'CreditPackages data seeded successfully.'
END
ELSE
BEGIN
    PRINT 'CreditPackages data already exists.'
END

-- Add PurchasedCredits column to UserProfiles if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'UserProfiles' AND COLUMN_NAME = 'PurchasedCredits')
BEGIN
    PRINT 'Adding PurchasedCredits column to UserProfiles...'
    ALTER TABLE [UserProfiles] ADD [PurchasedCredits] int NOT NULL DEFAULT 0;
    PRINT 'PurchasedCredits column added successfully.'
END
ELSE
BEGIN
    PRINT 'PurchasedCredits column already exists in UserProfiles.'
END

-- Verification queries
PRINT 'Verification:'
SELECT 'CreditPackages table' as TableName, COUNT(*) as RecordCount FROM [CreditPackages];
SELECT 'CreditPurchases table' as TableName, COUNT(*) as RecordCount FROM [CreditPurchases];

PRINT 'Emergency migration script completed successfully!'
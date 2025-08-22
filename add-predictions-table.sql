-- Add Predictions table if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Predictions')
BEGIN
    CREATE TABLE [Predictions] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Style] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Predictions] PRIMARY KEY ([Id])
    );
    
    CREATE NONCLUSTERED INDEX [IX_Predictions_UserId] ON [Predictions] ([UserId]);
    
    PRINT 'Predictions table created successfully'
END
ELSE
BEGIN
    PRINT 'Predictions table already exists'
END

-- Update migration history to mark migrations as applied
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT '__EFMigrationsHistory table created'
END

-- Mark both migrations as applied
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250808170932_InitialSQLServerFixed')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250808170932_InitialSQLServerFixed', '8.0.7');
    PRINT 'InitialSQLServerFixed migration marked as applied'
END

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250821035813_AddPredictionsTable')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250821035813_AddPredictionsTable', '8.0.7');
    PRINT 'AddPredictionsTable migration marked as applied'
END

PRINT 'Database migration completed successfully'
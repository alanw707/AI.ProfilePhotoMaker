-- EMERGENCY SCHEMA FIX FOR STAGING DATABASE
-- Execute this SQL directly on the aiprofilemaker-sql-staging database
-- This adds the missing columns that are causing HTTP 500 errors

USE aiprofilemakerdb;

-- Add missing columns to CreditPackages table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'Description')
    ALTER TABLE CreditPackages ADD Description nvarchar(500) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'DisplayOrder')
    ALTER TABLE CreditPackages ADD DisplayOrder int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'BonusCredits')
    ALTER TABLE CreditPackages ADD BonusCredits int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripeProductId')
    ALTER TABLE CreditPackages ADD StripeProductId nvarchar(max) NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'StripePriceId')
    ALTER TABLE CreditPackages ADD StripePriceId nvarchar(max) NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'CreditPackages' AND COLUMN_NAME = 'UpdatedAt')
    ALTER TABLE CreditPackages ADD UpdatedAt datetime2 NULL;

-- Update existing records with proper display order
UPDATE CreditPackages SET DisplayOrder = Id WHERE DisplayOrder = 0;

-- Update existing records with descriptions
UPDATE CreditPackages SET Description = 
    CASE 
        WHEN Name LIKE '%Starter%' THEN 'Perfect for trying out our service'
        WHEN Name LIKE '%Standard%' THEN 'Great value for regular users'
        WHEN Name LIKE '%Premium%' THEN 'Best value with bonus credits'
        ELSE 'Credit package for profile photo generation'
    END
WHERE Description = '';

-- Verify the fix
SELECT Id, Name, Credits, Price, Description, DisplayOrder, BonusCredits, IsActive 
FROM CreditPackages 
ORDER BY DisplayOrder;
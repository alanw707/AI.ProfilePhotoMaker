-- Update CreditPackages with rich descriptions from local SQLite database
-- This script populates the missing Description, DisplayOrder, and BonusCredits fields

-- Update Package 1: Starter Pack
UPDATE CreditPackages 
SET Description = 'Perfect for trying out custom training and styled generations',
    DisplayOrder = 1,
    BonusCredits = 0,
    UpdatedAt = GETUTCDATE()
WHERE Id = 1 AND Name = 'Starter Pack';

-- Update Package 2: Professional Pack  
UPDATE CreditPackages 
SET Description = 'Most popular - great for professionals',
    DisplayOrder = 2,
    BonusCredits = 30,
    UpdatedAt = GETUTCDATE()
WHERE Id = 2 AND Name = 'Professional Pack';

-- Update Package 3: Studio Pack
UPDATE CreditPackages 
SET Description = 'Best value for content creators and businesses',
    DisplayOrder = 3,
    BonusCredits = 100,
    UpdatedAt = GETUTCDATE()
WHERE Id = 3 AND Name = 'Studio Pack';

-- Alternative update by Name in case IDs don't match exactly
UPDATE CreditPackages 
SET Description = 'Perfect for trying out custom training and styled generations',
    DisplayOrder = 1,
    BonusCredits = 0,
    UpdatedAt = GETUTCDATE()
WHERE Name LIKE '%Starter%' AND (Description = '' OR Description IS NULL);

UPDATE CreditPackages 
SET Description = 'Most popular - great for professionals',
    DisplayOrder = 2,
    BonusCredits = 30,
    UpdatedAt = GETUTCDATE()
WHERE Name LIKE '%Professional%' AND (Description = '' OR Description IS NULL);

UPDATE CreditPackages 
SET Description = 'Best value for content creators and businesses',
    DisplayOrder = 3,
    BonusCredits = 100,
    UpdatedAt = GETUTCDATE()
WHERE Name LIKE '%Studio%' AND (Description = '' OR Description IS NULL);
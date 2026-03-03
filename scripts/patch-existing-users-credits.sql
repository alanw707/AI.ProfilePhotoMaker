-- Add 20 credits to existing users stuck at the old free tier amount
UPDATE "UserProfiles" 
SET "Credits" = "Credits" + 20
WHERE "Credits" = 5 
AND "SubscriptionTier" = 0; -- Basic tier

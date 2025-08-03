-- Script to populate missing styles in the database
-- Based on fallback data from landing.component.ts

-- Clear existing styles first (optional - uncomment if needed)
-- DELETE FROM Styles;

-- Insert the 20 expected styles
INSERT INTO Styles (name, description, isActive) VALUES
('professional-linkedin', 'Corporate professional headshot', 1),
('creative-professional', 'Artistic and modern look', 1),
('corporate-executive', 'C-suite leadership presence', 1),
('casual-professional', 'Approachable yet professional', 1),
('classic-headshot', 'Timeless professional look', 1),
('modern-professional', 'Cutting-edge style', 1),
('elegant-portrait', 'Refined and polished', 1),
('friendly-professional', 'Warm and welcoming', 1),
('confident-leader', 'Strong leadership presence', 1),
('artistic-expression', 'Creative industry focused', 1),
('business-casual', 'Perfect for most industries', 1),
('tech-professional', 'Tech industry optimized', 1),
('senior-executive', 'High-level executive presence', 1),
('professional-consultant', 'Expert and trustworthy', 1),
('entrepreneur', 'Visionary and forward-thinking', 1),
('academic-professional', 'Scholarly and approachable', 1),
('sales-professional', 'Trustworthy and engaging', 1),
('marketing-expert', 'Creative and strategic', 1),
('finance-professional', 'Analytical and precise', 1),
('healthcare-professional', 'Caring and competent', 1)
ON CONFLICT(name) DO NOTHING; -- Avoid duplicates if some already exist

-- Verify the insert
SELECT COUNT(*) as total_styles FROM Styles WHERE isActive = 1;
SELECT id, name, description FROM Styles ORDER BY id;
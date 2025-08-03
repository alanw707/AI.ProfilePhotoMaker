-- Update Styles with rich descriptions and prompt templates from insert_styles.sql
-- This script populates missing Description, PromptTemplate, and NegativePromptTemplate fields

-- First, add missing columns if they don't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'PromptTemplate')
  ALTER TABLE Styles ADD PromptTemplate nvarchar(max) NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'NegativePromptTemplate')
  ALTER TABLE Styles ADD NegativePromptTemplate nvarchar(max) NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Styles' AND COLUMN_NAME = 'UpdatedAt')
  ALTER TABLE Styles ADD UpdatedAt datetime2 NULL;

-- Update Corporate style
UPDATE Styles 
SET Description = 'Professional studio portrait in formal business attire with clean background',
    PromptTemplate = 'Professional studio portrait of a {gender} in formal business attire, clean background, confident expression, corporate office lighting, sharp focus',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, inappropriate attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'corporate';

-- Update Executive style
UPDATE Styles 
SET Description = 'High-end executive portrait with power pose and luxury office background',
    PromptTemplate = 'High-end executive portrait of a {gender}, power pose, elegant business suit, luxury office background, natural light, serious expression, premium look',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual clothing, unprofessional setting',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'executive';

-- Update Consultant style
UPDATE Styles 
SET Description = 'Friendly consultant portrait in smart-casual attire with approachable expression',
    PromptTemplate = 'Portrait of a friendly {gender} consultant in semi-formal smart-casual attire, clean background, approachable expression, modern professional tone',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, overly formal attire, intimidating expression',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'consultant';

-- Update LinkedIn style
UPDATE Styles 
SET Description = 'Professional LinkedIn-style headshot with confident and warm expression',
    PromptTemplate = 'Professional LinkedIn-style headshot of a {gender}, neutral background, confident and warm smile, clean business-casual attire, high clarity',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, full body shot, distracting background',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'linkedin';

-- Update Legal style
UPDATE Styles 
SET Description = 'Formal lawyer portrait in courtroom or law office setting',
    PromptTemplate = 'Formal portrait of a {gender} lawyer in courtroom or law office, dark tailored suit, serious expression, soft shadows, bookshelf or columns in background',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual setting, inappropriate attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'legal';

-- Update Medical style
UPDATE Styles 
SET Description = 'Healthcare professional portrait with lab coat and trustworthy expression',
    PromptTemplate = 'Portrait of a {gender} healthcare professional in lab coat, stethoscope, hospital or clinic background, calm and trustworthy expression, soft light',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, inappropriate medical setting',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'medical';

-- Update Author style
UPDATE Styles 
SET Description = 'Intellectual author portrait with bookshelves or writing desk background',
    PromptTemplate = 'Intellectual portrait of a {gender} with bookshelves or writing desk in the background, warm ambient lighting, thoughtful gaze, creative professional styling',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'author';

-- Update Entrepreneur style
UPDATE Styles 
SET Description = 'Modern startup founder portrait in co-working space with confident energy',
    PromptTemplate = 'Modern portrait of a {gender} startup founder in a co-working space or minimalist office, tech-savvy outfit, confident energy, natural lighting',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated office, traditional formal attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'entrepreneur';

-- Update Startup style
UPDATE Styles 
SET Description = 'Casual-smart startup founder with t-shirt and blazer combination',
    PromptTemplate = 'Casual-smart headshot of a {gender} in a t-shirt and blazer, clean tech-style background, bright lighting, relaxed smile, startup founder vibe',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, overly formal suit',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'startup';

-- Update Tech Professional style
UPDATE Styles 
SET Description = 'Tech professional portrait with laptop or code in background',
    PromptTemplate = 'Portrait of a {gender} tech professional in modern outfit, with a laptop or code in background, neutral tones, focused expression, digital workspace',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, outdated technology',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'tech-professional' OR Name = 'tech professional';

-- Update Influencer style
UPDATE Styles 
SET Description = 'Trendy social media influencer portrait with engaging eye contact',
    PromptTemplate = 'Trendy portrait of a {gender} social media influencer with engaging eye contact, soft lighting, fashionable outfit, blurred background, Instagram vibe',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, overly professional attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'influencer';

-- Update Digital Nomad style
UPDATE Styles 
SET Description = 'Outdoor lifestyle portrait of remote worker with laptop in natural setting',
    PromptTemplate = 'Outdoor lifestyle portrait of a {gender} remote worker, natural lighting, beach or mountain cafe background, laptop in view, relaxed expression',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, indoor office setting',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'digital-nomad' OR Name = 'digital nomad';

-- Update Creative style
UPDATE Styles 
SET Description = 'Colorful and dynamic artist portrait with creative studio background',
    PromptTemplate = 'Colorful and dynamic portrait of a {gender} artist or creative, expressive pose, vibrant lighting, creative studio background, bold composition',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, boring background, corporate attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'creative';

-- Update Casual style
UPDATE Styles 
SET Description = 'Natural lifestyle photo in everyday clothing with warm lighting',
    PromptTemplate = 'Natural lifestyle photo of a {gender} in everyday clothing, warm lighting, soft expression, home or park background, candid feel',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, formal business attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'casual';

-- Update Artistic style
UPDATE Styles 
SET Description = 'Fine art portrait with dramatic lighting and stylized clothing',
    PromptTemplate = 'Fine art portrait of a {gender} in dramatic lighting, stylized clothing, moody background, painterly composition, thoughtful gaze',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, plain background, conventional lighting',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'artistic';

-- Update Edgy Urban style
UPDATE Styles 
SET Description = 'Street-style portrait with gritty city background and edgy aesthetic',
    PromptTemplate = 'Street-style portrait of a {gender}, gritty city background, bold outfit, high contrast lighting, strong pose, edgy aesthetic',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, clean corporate setting, formal attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'edgy-urban' OR Name = 'edgy/urban';

-- Update Glamour style
UPDATE Styles 
SET Description = 'Fashion-inspired glamorous portrait with studio lighting and luxury feel',
    PromptTemplate = 'Fashion-inspired portrait of a {gender} in glamorous makeup and clothing, studio lighting, soft glow effect, luxury editorial feel',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual everyday clothing',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'glamour';

-- Update Academic style
UPDATE Styles 
SET Description = 'Scholar portrait with books or chalkboard in academic setting',
    PromptTemplate = 'Portrait of a {gender} scholar with books or chalkboard in background, glasses, thoughtful expression, classic academic setting and lighting',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'academic';

-- Update Fitness style
UPDATE Styles 
SET Description = 'Athletic portrait in workout gear with energetic expression',
    PromptTemplate = 'Athletic portrait of a {gender} in workout gear, gym or outdoor fitness location, strong pose, energetic expression, high contrast lighting',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, formal business attire',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'fitness';

-- Update Spiritual style
UPDATE Styles 
SET Description = 'Serene portrait in natural light with peaceful spiritual elements',
    PromptTemplate = 'Serene portrait of a {gender} in natural light, peaceful outdoor or temple-like setting, soft expression, spiritual elements like beads or robes',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, busy commercial setting',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'spiritual';

-- Update Fashion style (if it exists)
UPDATE Styles 
SET Description = 'High-fashion portrait with dramatic lighting and designer clothing',
    PromptTemplate = 'High-fashion portrait of a {gender} in designer clothing, dramatic lighting, studio background, bold pose, editorial style',
    NegativePromptTemplate = 'deformed iris, deformed pupils, semi-realistic, cgi, 3d, render, sketch, cartoon, drawing, anime, mutated hands and fingers, deformed, distorted, disfigured, poorly drawn, bad anatomy, wrong anatomy, extra limb, missing limb, floating limbs, disconnected limbs, mutation, mutated, ugly, disgusting, blurry, amputation, casual everyday clothing',
    UpdatedAt = GETUTCDATE()
WHERE Name = 'fashion';
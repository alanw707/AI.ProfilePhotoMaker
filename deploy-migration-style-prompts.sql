BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [CreditPackages] SET [CreatedAt] = ''2026-02-20T13:21:07.5638739Z''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [CreditPackages] SET [CreatedAt] = ''2026-02-20T13:21:07.5638741Z''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [CreditPackages] SET [CreatedAt] = ''2026-02-20T13:21:07.5638743Z''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638895Z'', [Description] = N''Sun-kissed vacation mode portrait'', [Name] = N''beach-vibes'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, winter clothes, cold weather, indoor office, formal business attire, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed'', [PromptTemplate] = N''{subject}, professional portrait of {gender} {ethnicity}, subtle beach vacation aesthetic, sun-kissed healthy glow, soft coastal background with blurred ocean hints, warm golden hour lighting, relaxed confident expression, casual summer style, natural beachy hair texture, healthy natural skin, even skin tone, natural skin texture, minimal retouching, head-and-shoulders framing'', [UpdatedAt] = ''2026-02-20T13:21:07.5638896Z''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638899Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, casual streetwear, coworking space, cafe, classroom, lecture hall, library, bookshelves, campus, influencer glam, fashion editorial, nightclub, beach, neon lighting, playful pose, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638900Z''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638901Z'', [Description] = N''Clean energetic refreshed portrait'', [Name] = N''fresh'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, tired, exhausted, dull skin, dark shadows, heavy makeup, messy appearance, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching'', [PromptTemplate] = N''{subject}, professional portrait of {gender} {ethnicity}, fresh clean aesthetic, dewy glowing skin, bright airy background, soft natural lighting, energetic refreshed expression, healthy vitality, natural skin texture, minimal retouching, head-and-shoulders framing'', [UpdatedAt] = ''2026-02-20T13:21:07.5638902Z''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638903Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, hoodie, t-shirt, tank top, athletic wear, coworking space, outdoor, park, city street, campus, library, bookshelves, lecture hall, cluttered background, busy background, neon lighting, cyberpunk, synthwave, fashion editorial, nightclub, beach, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638904Z''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638905Z'', [NegativePromptTemplate] = N''harsh spotlight, overexposed face, blown highlights, magenta skin, neon wash on face, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, cyberpunk armor, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638906Z''
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638907Z'', [NegativePromptTemplate] = N''unprofessional attire, harsh expression, inappropriate background, distracting elements, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text'', [UpdatedAt] = ''2026-02-20T13:21:07.5638908Z''
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638909Z'', [NegativePromptTemplate] = N''harsh spotlight, front lighting, on-camera flash, direct key light, frontal key, beauty lighting, glamour retouch, overexposed face, blown highlights, neon wash on face, magenta skin, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, oily skin, exaggerated makeup, club strobe lighting, cyberpunk, sci-fi helmet, sunglasses, hat, visible logos, distorted face, bad anatomy, extra fingers, bad hands, watermark, text, HDR, oversharpened, formal suit, suit and tie, tuxedo, casual daywear, bright daylight, morning light, workout clothes, plain appearance, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638910Z''
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638911Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit and tie, corporate boardroom, conservative law firm vibe, stiff studio headshot, doctor coat, medical scrubs, influencer glam, nightclub, beach, workout clothes, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638912Z''
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638913Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, formal suit, tie, tuxedo, corporate boardroom, traditional office, stiff studio pose, luxury executive vibe, courthouse, doctor coat, medical scrubs, neon cyberpunk, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638915Z''
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638916Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit and tie, tuxedo, hoodie, coworking space, startup founder vibe, boardroom, courthouse, doctor coat, medical scrubs, neon lighting, cyberpunk, synthwave, heavy color gels, nightclub, beach, influencer glam, full body shot, watermark, text, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638917Z''
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638918Z'', [NegativePromptTemplate] = N''formal business wear, rigid posture, corporate setting, boring expression, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638919Z''
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638920Z'', [NegativePromptTemplate] = N''formal suit, rigid corporate setting, stiff posture, traditional office background, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, full body shot, watermark, text, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638920Z''
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638922Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate suit, suit and tie, boardroom, LinkedIn headshot, plain gray background, courthouse, doctor coat, medical scrubs, bohemian costume, hippie, festival, oil painting, illustration, sketch, cartoon, anime, cyberpunk, synthwave, neon lighting, heavy color gels, glamour makeup, nightclub, beach, graffiti, leather jacket, streetwear, selfie, ring light, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638922Z''
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638924Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, suit, tie, blazer, formal business attire, corporate headshot, boardroom, studio backdrop, stiff pose, arms crossed, cold expression, luxury executive vibe, courthouse, doctor coat, medical scrubs, beachwear, nightclub, neon lighting, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, bare torso, topless, no shirt, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638924Z''
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638926Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate headshot, LinkedIn, suit and tie, boardroom, modern office, coworking, hoodie, influencer, ring light, selfie, bright flat lighting, neon cyberpunk, synthwave, beach, nightclub, glamour makeup, fashion editorial, illustration, sketch, cartoon, anime, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, poreless skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, exaggerated wrinkles, overly deep wrinkles, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638926Z''
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638928Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, conservative formal, traditional business, bland conventional, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638928Z''
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638931Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, casual simple, plain appearance, understated look, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638931Z''
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638933Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, noise, artifacts, distorted face, bad anatomy, extra fingers, bad hands, corporate boardroom, high-rise office, executive suite, hoodie, streetwear, nightclub, beach, neon lighting, plain backdrop, blank wall, studio backdrop, fashion editorial, glamour makeup, forced grin, exaggerated smile, grimace, open mouth, tongue, extreme head tilt, multiple watches, watch on both wrists, excessive bracelets, oversized jewelry, sunglasses, hat, visible logos, full body action, dramatic gestures, arms flailing, unnatural hand positions, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, blown highlights, overexposed face, harsh facial shadows, HDR, oversharpened, too much clarity, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638933Z''
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638935Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, sedentary look, unhealthy appearance, low energy, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching'', [UpdatedAt] = ''2026-02-20T13:21:07.5638935Z''
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    EXEC(N'UPDATE [Styles] SET [CreatedAt] = ''2026-02-20T13:21:07.5638937Z'', [NegativePromptTemplate] = N''blurry, low quality, out of focus, distorted face, bad anatomy, extra fingers, bad hands, outdated technology, old fashioned, formal business, analog aesthetic, traditional office, full body shot, watermark, text, waxy skin, plastic skin, airbrushed skin, over-smoothed skin, beauty filter, heavy retouching, shirtless, bare chest, topless, nude, undressed'', [UpdatedAt] = ''2026-02-20T13:21:07.5638937Z''
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260220132108_FixStylePromptsDataDriftAndQualityAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260220132108_FixStylePromptsDataDriftAndQualityAudit', N'8.0.16');
END;
GO

COMMIT;
GO


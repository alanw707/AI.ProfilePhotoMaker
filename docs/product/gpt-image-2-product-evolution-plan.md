# GPT Image 2 Product Evolution Plan

Date: 2026-05-11
Status: Strategic plan for review
Product: AI Profile Photo Maker

## Executive Summary

GPT Image 2 materially changes the market for AI headshots. The old product pattern of asking users to upload many photos, train a custom model, wait, and then generate batches is becoming less compelling because modern image editing/generation models can create strong professional portraits from far fewer inputs.

AI Profile Photo Maker should evolve from a generic "AI headshot generator" into an **AI Personal Brand Photo Studio**.

The product should not compete with OpenAI as a raw image model. Instead, it should compete on workflow, taste, quality control, packaging, and finished professional outcomes.

Core shift:

- Old promise: Upload many selfies, train a model, wait for headshots.
- New promise: Upload one selfie, choose your professional goal, and get a complete platform-ready photo kit in minutes.

## Strategic Thesis

Raw image generation is becoming commoditized. Users can access strong image models directly through ChatGPT or the OpenAI API. The defensible product value must move up the stack.

AI Profile Photo Maker should own the outcome:

- LinkedIn-ready profile photos
- Resume-ready headshots
- Website bio images
- Founder/speaker photos
- Realtor, doctor, consultant, and team-page assets
- Professionally cropped and packaged downloads
- Guided refinement without prompt engineering

OpenAI gives users capability. AI Profile Photo Maker should give users a finished, polished professional photo kit.

## Why Users Would Still Pay Instead of Using OpenAI Directly

Most users do not want to:

- write prompts
- manage API keys or usage billing
- understand model parameters
- know which photo styles work professionally
- generate dozens of bad attempts
- judge face likeness and realism
- crop images for LinkedIn, resumes, avatars, websites, and team pages
- fix backgrounds, clothing, lighting, or overly-AI artifacts
- decide what looks credible for their profession
- download and organize multiple platform-specific assets

AI Profile Photo Maker should sell convenience, taste, confidence, and done-for-you outputs.

The value proposition becomes:

> We turn your selfie into ready-to-use professional identity assets without prompts, guesswork, or manual editing.

## New Product Positioning

### Recommended Category

**AI Personal Brand Photo Studio**

### Recommended Messaging

Possible hero headlines:

- Turn one selfie into a complete professional photo kit.
- Studio-quality profile photos without a photoshoot.
- Create LinkedIn, resume, and website-ready photos in minutes.
- Your personal brand photo studio, powered by AI.

### Messaging to Avoid

Reduce or remove primary emphasis on:

- model training
- uploading 10-20 photos
- waiting for a model
- generic AI headshot generation
- technical model language

Users care about looking credible, polished, and professional. They do not care which model generated the result unless it improves trust.

## Product Pillars

### 1. Instant Single-Photo Generation

The new default flow should require one good selfie.

Recommended user flow:

1. Upload one clear selfie.
2. The app checks photo quality.
3. User chooses a professional goal.
4. User chooses a style or vibe.
5. The system generates several professional outputs.
6. The user refines, downloads, or creates platform-specific exports.

The existing multi-photo/training flow can remain temporarily as an advanced option, but it should no longer be the main default.

Recommended framing:

> One selfie is enough to start. Add more photos only if you want stronger identity accuracy.

### 2. Goal-First Onboarding

Instead of starting with "upload photos to train your model," the product should ask:

> Where will you use these photos?

Suggested goals:

- LinkedIn
- Resume/job search
- Founder profile
- Realtor profile
- Doctor/dentist profile
- Consultant profile
- Speaker bio
- Company team page
- Dating profile
- Social avatar

This lets the product choose better defaults for crop, background, outfit, tone, and download package.

### 3. Curated Style Presets

The product should hide prompt complexity behind professionally curated presets.

Suggested presets:

- Executive
- Friendly professional
- Founder/startup
- Modern corporate
- Creative professional
- Realtor friendly
- Medical/dental clean
- Consultant/premium
- Speaker/conference
- Outdoor natural
- Minimal studio
- Warm approachable

Each preset should map to:

- prompt template
- negative constraints
- outfit guidance
- background guidance
- crop/framing rules
- output dimensions
- quality checks

### 4. Quality Control Layer

This is one of the most important differentiators.

After images are generated, the system should rank and filter them before showing the user the final gallery.

Recommended checks:

- face is visible
- eyes are open
- no distorted facial features
- no extra hands, limbs, accessories, or artifacts
- realistic skin texture
- professional framing
- no text, logos, or watermarks
- acceptable face similarity against the uploaded image
- LinkedIn/resume-safe crop
- no overly glossy AI look

UX copy idea:

> We filtered out weak generations and ranked your best results first.

This turns raw image generation into a managed professional workflow.

### 5. Refinement Buttons Instead of Prompt Boxes

Most users should not need to write prompts.

Suggested quick actions:

- More natural
- More confident
- More approachable
- More executive
- Less corporate
- Change outfit
- Change background
- Better lighting
- Reduce AI look
- Try different angle
- Softer smile
- More premium
- More casual

Advanced users can still have an optional text prompt, but the default UX should use buttons.

### 6. Download-Ready Asset Packs

The product should sell finished kits, not just generated images.

Suggested packages:

#### LinkedIn Kit

- LinkedIn profile photo
- square avatar
- banner/cover option
- recruiter-friendly version
- transparent background option

#### Job Search Kit

- resume headshot
- LinkedIn crop
- email avatar
- portfolio crop

#### Founder Kit

- website bio photo
- press/speaker photo
- LinkedIn profile image
- social avatar
- optional cover/banner asset

#### Realtor Kit

- MLS crop
- business card photo
- website bio image
- social profile image
- friendly/local-market style variations

#### Medical/Dental Kit

- provider profile photo
- clinic website crop
- appointment platform image
- LinkedIn crop
- white coat/professional attire variations

This creates a higher perceived value than a folder of generic PNGs.

## Recommended New User Journey

### Step 1: Landing Page

Primary promise:

> Create a complete professional photo kit from one selfie.

Above-the-fold story:

1. Upload selfie.
2. Choose professional goal/style.
3. Download ready-to-use photos.

Primary CTA:

> Create my photo kit

### Step 2: Upload

Require one photo to start.

Photo quality guidance:

- good lighting
- face visible
- no sunglasses
- front-facing preferred
- high resolution recommended

If the uploaded photo is weak, show a warning:

> This photo may produce weaker results. Upload a clearer selfie for better likeness.

### Step 3: Choose Goal

Ask:

> Where will you use these photos?

This controls package defaults and result presentation.

### Step 4: Choose Style

Show visual cards for professional styles instead of a prompt field.

### Step 5: Generate

Progress language should say:

> Creating your professional photo kit...

Avoid:

> Training your model...

### Step 6: Results

Group results by outcome:

- Best LinkedIn photo
- Best resume photo
- Best website photo
- Best avatar

Each result should support:

- Download
- Refine
- Make variation
- Change background
- Save favorite

### Step 7: Upsell

Possible conversion model:

- free or low-cost preview
- watermarked previews
- pay to unlock HD downloads
- upsell full kit, extra styles, or advanced refinements

## Backend Architecture Plan

### Keep Existing System Temporarily

Do not remove the current training system immediately.

Reposition it as:

- Legacy mode
- Advanced accuracy mode
- Multi-photo identity mode

The new default should be GPT Image 2 instant generation once quality is validated.

### Add Image Provider Abstraction

Create or extend a backend abstraction so GPT Image 2 is one provider, not hardcoded throughout the app.

Suggested interface concept:

- `IImageGenerationProvider`
- `OpenAiGptImageProvider`
- existing generation/training provider as fallback

This protects the product if:

- OpenAI pricing changes
- another model becomes better
- rate limits become an issue
- quality changes
- A/B testing is needed

### Add Prompt Template System

Prompt templates should be structured by:

- professional goal
- style preset
- outfit
- background
- expression
- crop/framing
- output dimensions
- negative constraints

Example internal prompt direction:

> Create a realistic professional headshot of the person in the reference image. Preserve facial identity, age, skin tone, and natural facial features. Style: modern LinkedIn executive portrait. Outfit: navy blazer and white shirt. Background: softly blurred modern office. Lighting: flattering studio lighting. Expression: confident and approachable. Avoid over-smoothing, plastic skin, distorted features, extra accessories, text, logos, or unrealistic AI artifacts.

### Add Scoring and Filtering Pipeline

Recommended post-generation steps:

1. Save raw outputs.
2. Detect face presence.
3. Compare face similarity to uploaded image.
4. Validate crop/framing.
5. Run quality/artifact checks.
6. Rank results.
7. Hide failures by default.
8. Let user request replacement generations.

### Add Asset Pack Generation

Once a user selects a favorite image, generate derivatives:

- LinkedIn square crop
- resume crop
- website bio crop
- square avatar
- transparent background
- high-resolution download
- optional banner/cover image
- ZIP package

Many of these outputs can be deterministic image-processing steps rather than expensive AI generations.

## Pricing Strategy

Move away from model-training package language and toward photo kits, credits, and professional outputs.

### Free Preview

- upload one image
- generate low-resolution previews
- watermark or limited download
- pay to unlock HD assets

### Starter: $9-$15

- one photo kit
- limited generated images
- HD downloads
- basic styles

### Pro: $19-$29

- multiple photo kits
- all platform crops
- background changes
- outfit variations
- refinement actions

### Premium: $49+

- higher generation volume
- advanced identity accuracy
- multi-photo input option
- priority processing
- more styles and refinements

### Team/Business Package

This may become the strongest revenue path.

Possible price range:

- $99-$499 depending on team size and included outputs

Features:

- invite team members
- consistent company style/background
- shared brand kit
- bulk downloads
- admin dashboard
- team page-ready exports

## Differentiation Moat

The model is not the moat.

The moat should be:

1. Workflow
   - no prompts
   - goal-based generation
   - guided results

2. Taste
   - curated presets
   - professional defaults
   - industry-specific styles

3. Quality control
   - bad generations filtered automatically
   - face match ranking
   - realism checks

4. Asset packaging
   - LinkedIn, resume, website, avatar, and team-page exports

5. Vertical specialization
   - realtor, dental, medical, founder, job seeker, consultant

6. Brand consistency
   - especially valuable for teams and small businesses

7. Trust
   - privacy controls
   - deletion controls
   - no API setup
   - clear usage rights

## Landing Page Change Plan

The landing page should reduce emphasis on training and increase emphasis on instant professional outcomes.

Recommended sections:

1. Hero
   - Headline: Create a complete professional photo kit from one selfie.
   - CTA: Create my photo kit.

2. Three-step flow
   - Upload selfie.
   - Choose your professional style.
   - Download ready-to-use photos.

3. Use cases
   - LinkedIn
   - Resume
   - Website
   - Realtor
   - Doctor/dentist
   - Founder
   - Team page

4. Before/after examples
   - show realistic transformations, not overly polished AI images

5. What you get
   - profile photo
   - resume crop
   - website photo
   - avatar
   - background variations
   - outfit options

6. Why not use a generic AI tool?
   - no prompts
   - professional presets
   - quality filtering
   - platform-ready exports
   - privacy controls

7. Pricing
   - photo kits and HD unlocks, not model-training packages

8. FAQ
   - Do I need multiple photos?
   - Can I change outfit/background?
   - Do the photos look like me?
   - Can I use this for LinkedIn?
   - Is my photo deleted?

## MVP Roadmap

### Phase 1: Strategic Pivot and Messaging

Goal: make the product no longer feel obsolete.

Deliverables:

- rewrite landing page copy
- reduce training-first language
- introduce "one selfie" promise where supported
- add use-case cards
- add "photo kit" framing
- update pricing language around downloads/kits

### Phase 2: GPT Image 2 Instant Prototype

Goal: validate quality before a full rebuild.

Deliverables:

- backend GPT Image 2 provider
- single-image upload endpoint
- 3-5 prompt templates
- generate 4-8 results per request
- save outputs
- show outputs in dashboard
- basic HD download path

### Phase 3: Guided Workflow

Goal: replace training-first UX for new users.

Deliverables:

- upload step
- goal selection
- style selection
- generation progress
- results gallery
- refinement buttons

### Phase 4: Quality Ranking

Goal: create real product differentiation.

Deliverables:

- face detection
- face similarity scoring
- crop/framing validation
- artifact/quality checks
- auto-hide failed generations
- ranked best picks

### Phase 5: Asset Packs

Goal: make the output feel worth paying for.

Deliverables:

- LinkedIn crop
- resume crop
- square avatar
- website bio crop
- transparent background option
- ZIP download

### Phase 6: Vertical Packs

Goal: improve conversion, SEO, and differentiation.

Recommended first verticals:

- LinkedIn/job seeker
- realtor
- doctor/dentist
- founder/entrepreneur

Each vertical should get:

- landing page
- style presets
- sample outputs
- tailored pricing copy
- SEO keywords

### Phase 7: Team/Business Product

Goal: unlock higher-ticket revenue.

Deliverables:

- invite team members
- shared brand style
- consistent background
- admin dashboard
- bulk download
- per-seat or package pricing

## Validation Plan

Before doing a full rebuild, run a focused quality and economics test.

Recommended test:

1. Collect 10-20 diverse test selfies.
2. Generate multiple styles through GPT Image 2.
3. Score outputs on:
   - likeness
   - realism
   - professional usefulness
   - artifact rate
   - cost per usable image
   - latency
4. Compare results against the current training pipeline.
5. Decide whether instant mode becomes default.

Success criteria:

- one-photo flow produces at least 3 usable professional outputs per user in most cases
- results look realistic and not overly AI-generated
- cost per paid kit supports healthy gross margin
- latency feels fast enough for an interactive workflow

## Key Risks and Mitigations

### Risk: OpenAI direct usage commoditizes simple generation

Mitigation:

- focus on finished professional kits
- add guided workflows
- add quality ranking
- add platform-ready exports

### Risk: Identity preservation may still fail sometimes

Mitigation:

- generate multiple outputs
- rank by face similarity
- allow optional multi-photo accuracy mode
- hide failed generations by default

### Risk: Generation costs hurt margins

Mitigation:

- limit free previews
- watermark previews
- charge for HD unlocks
- cap refinements by tier
- use deterministic crops where possible

### Risk: AI headshot market is crowded

Mitigation:

- specialize by vertical
- target teams and small businesses
- sell asset kits rather than generic headshots

### Risk: Users distrust AI-looking outputs

Mitigation:

- make natural realism the default style
- reject glossy/plastic outputs
- show realistic before/after examples
- add privacy and deletion messaging

## Recommended First Move

Do not start with a full rewrite.

Recommended sprint:

1. Build a private GPT Image 2 instant-generation prototype.
2. Test it against 10-20 diverse selfies.
3. Measure likeness, realism, artifact rate, latency, and cost.
4. If quality is strong, redesign onboarding around instant generation.
5. Keep the current training flow as fallback until the new path proves better.

## Strategic Decision

AI Profile Photo Maker should evolve from:

> AI headshot generator

into:

> AI profile photo kit builder

This gives users a reason to pay even when raw image generation is available elsewhere. OpenAI owns the model. AI Profile Photo Maker should own the professional outcome.

# Instant portrait styles use existing prompts without model training

We will make portrait style selection the primary `/enhance` generation choice by reusing existing active `Style.PromptTemplate` records and preview images with OpenAI Images 2, rather than returning to Replicate custom model training. This keeps the workflow instant, preserves the old style catalog users recognize, and accepts less training-based identity consistency in exchange for speed, simpler operations, and lower product complexity.

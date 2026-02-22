import { BlogPost } from './blog.types';

export const blogPosts: BlogPost[] = [
  {
    slug: 'ai-headshot-guide',
    title: 'The Practical AI Headshot Guide (No BS) — Photos, Prompts, and Mistakes to Avoid',
    description:
      'A practical, step-by-step guide to getting better AI headshots: source photos, outfits, lighting, prompt tips, and how to avoid uncanny results.',
    dateIso: '2026-02-22T00:00:00.000Z',
    author: 'AI Profile Photo Maker',
    tags: ['AI headshots', 'LinkedIn', 'photos', 'prompts'],
    contentHtml: `
      <p>If you’ve tried AI headshots and thought “why do I look… kinda like me, but also like a wax statue?”, this is for you.</p>
      <p>This guide is built from what consistently works: good source photos + sane style choices + a few prompt guardrails.</p>

      <h2>1) Start with the right source photos</h2>
      <ul>
        <li><strong>Use 6–12 photos</strong> with different angles (front, 3/4, slight left/right).</li>
        <li><strong>Lighting:</strong> bright indirect light beats harsh overhead lighting.</li>
        <li><strong>Expression:</strong> neutral or slight smile. Avoid exaggerated faces.</li>
        <li><strong>Resolution:</strong> sharper is better. Avoid heavy filters.</li>
      </ul>

      <h2>2) What to wear (and what to avoid)</h2>
      <ul>
        <li><strong>Solid colors</strong> and simple textures look more realistic.</li>
        <li>Avoid busy patterns, logos, and thin stripes (they create artifacts).</li>
        <li>For LinkedIn: blazer or collared shirt is safe. For founders: clean t-shirt + jacket works.</li>
      </ul>

      <h2>3) Background + framing rules</h2>
      <ul>
        <li>Prefer simple backgrounds (studio gray, soft office blur, neutral wall).</li>
        <li>Frame from chest up. Too tight = uncanny. Too wide = weird hands/arms show up.</li>
      </ul>

      <h2>4) Prompt tips that actually matter</h2>
      <ul>
        <li>Ask for: <em>"professional headshot, natural skin texture, realistic lighting"</em>.</li>
        <li>Avoid: <em>"perfect skin, flawless, model"</em> (usually creates plastic skin).</li>
        <li>If you get artifacts: add <em>"no blur, no extra fingers, no distortion"</em>.</li>
      </ul>

      <h2>5) The 5 most common mistakes</h2>
      <ol>
        <li>Uploading only selfies with the same angle</li>
        <li>Low light / noisy images</li>
        <li>Heavy beauty filters</li>
        <li>Over-stylized prompts ("cinematic", "hyperreal", etc.)</li>
        <li>Expecting every output to be perfect — curate the best 5–15%</li>
      </ol>

      <h2>6) Quick checklist</h2>
      <ul>
        <li>✅ 6–12 clear photos</li>
        <li>✅ simple outfit</li>
        <li>✅ simple background</li>
        <li>✅ realistic prompt wording</li>
      </ul>

      <p>If you want, try the workflow on <a href="/">AI Profile Photo Maker</a> and use this checklist as your baseline.</p>
    `.trim(),
  },
];

export function getBlogPost(slug: string): BlogPost | undefined {
  return blogPosts.find(p => p.slug === slug);
}

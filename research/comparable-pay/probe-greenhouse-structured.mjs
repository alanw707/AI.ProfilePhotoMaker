// Bounded, read-only field probe. No job text, identity, or pay amount is retained.
// Greenhouse documents pay_input_ranges on GET /jobs/{id}?pay_transparency=true.
const boards=['airbnb','asana','databricks','reddit','robinhood','instacart','figma','dropbox','stripe','coinbase','discord','gusto','twilio','cloudflare'];
const PER_BOARD=8;
async function getJson(url){
  const controller=new AbortController();
  const timer=setTimeout(()=>controller.abort(),12000);
  try {
    const response=await fetch(url,{signal:controller.signal});
    if(!response.ok)return {status:response.status};
    return {status:response.status,data:await response.json()};
  } finally { clearTimeout(timer); }
}
function candidate(job){
  const title=String(job.title||'').toLowerCase();
  const location=String(job.location?.name||'').toLowerCase();
  return /software engineer|application engineer|software developer|backend engineer|frontend engineer/.test(title)
    && /san francisco|bay area/.test(location)
    && !/remote|multiple|united states|anywhere/.test(location);
}
const results=[];
for(const board of boards){
  try {
    const list=await getJson(`https://boards-api.greenhouse.io/v1/boards/${board}/jobs`);
    if(!list.data){results.push({board,status:list.status});continue;}
    const matching=(list.data.jobs||[]).filter(candidate);
    const sample=matching.slice(0,PER_BOARD);
    const details=await Promise.all(sample.map(job=>getJson(`https://boards-api.greenhouse.io/v1/boards/${board}/jobs/${job.id}?pay_transparency=true`).catch(error=>({error:error.name}))));
    let withRanges=0,validUsd=0,unknownBasis=0;
    for(const detail of details){
      const ranges=detail.data?.pay_input_ranges;
      if(!Array.isArray(ranges)||!ranges.length)continue;
      withRanges++;
      const valid=ranges.some(range=>range.currency_type==='USD'&&Number.isFinite(range.min_cents)
        && Number.isFinite(range.max_cents)&&range.min_cents>0&&range.max_cents>=range.min_cents);
      if(valid)validUsd++;
      // The documented range object has no normalized annual/hourly basis field.
      if(valid)unknownBasis++;
    }
    results.push({board,status:list.status,matchingSoftwareSf:matching.length,sampled:sample.length,
      withStructuredPayRanges:withRanges,validUsdRanges:validUsd,annualBasisUnknown:unknownBasis,
      detailErrors:details.filter(x=>x.error||!x.data).length});
  }catch(error){results.push({board,error:error.name});}
}
console.log(JSON.stringify({retrievedAt:new Date().toISOString(),source:'Public Greenhouse Job Board GET endpoints',sampleLimitPerBoard:PER_BOARD,results,
  limits:['First matching posts per board only; not a representative national sample.',
    'Structured min/max cents are not normalized to annual pay without explicit basis.',
    'Public GET access does not grant commercial cross-employer aggregation or retention rights.',
    'No job text, identity, or observed pay value is saved by this probe.']},null,2));

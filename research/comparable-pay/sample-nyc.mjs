// Read-only public-data sample. Prints aggregates and short hashed IDs only.
import {createHash} from 'node:crypto';
import {evaluate} from './cohort.mjs';
const url=new URL('https://data.cityofnewyork.us/resource/kpav-sd4t.json');
url.searchParams.set('$select','job_id,agency,business_title,job_category,level,career_level,salary_range_from,salary_range_to,salary_frequency,full_time_part_time_indicator,work_location,posting_type,posting_date,post_until');
url.searchParams.set('$where',"posting_type='External'");
url.searchParams.set('$limit','5000');
const response=await fetch(url,{headers:{'User-Agent':'AIProfilePhotoMaker-career-research/0.1 (read-only qualification)'}});
if(!response.ok)throw new Error('NYC Open Data request failed: '+response.status);
const rows=await response.json();
function family(text){
  const s=String(text||'').toLowerCase();
  if(/software|information technology|application develop|programmer|technology/.test(s))return 'software';
  if(/nurs|health|medical|clinical/.test(s))return 'healthcare';
  if(/operations|program manager|administrative|project manager/.test(s))return 'operations';
  return null;
}
function borough(text){
  const s=String(text||'').toLowerCase();
  if(/bronx/.test(s))return 'Bronx, NY';
  if(/brooklyn/.test(s))return 'Brooklyn, NY';
  if(/queens/.test(s))return 'Queens, NY';
  if(/staten island/.test(s))return 'Staten Island, NY';
  if(/manhattan|new york ny|broadway|worth street/.test(s))return 'Manhattan, NY';
  return null;
}
const now=new Date();
const records=rows.map(r=>({
  id:String(r.job_id||''),canonicalId:String(r.job_id||''),employer:'City of New York',
  agency:r.agency||'Unknown city agency',
  role:family((r.job_category||'')+' '+(r.business_title||'')),
  geography:borough(r.work_location),level:r.career_level||r.level||null,
  employmentType:r.full_time_part_time_indicator==='F'?'full-time':r.full_time_part_time_indicator==='P'?'part-time':null,
  eligible:null,employerDisclosed:true,
  currency:'USD',basis:String(r.salary_frequency||'').toLowerCase(),
  low:Number(r.salary_range_from),high:Number(r.salary_range_to),updatedAt:r.posting_date||'',
  sampleHash:createHash('sha256').update(String(r.job_id||'')).digest('hex').slice(0,12)
}));
const families=['software','healthcare','operations'];
const geographies=[...new Set(records.map(x=>x.geography).filter(Boolean))];
const summaries=[];
for(const role of families)for(const geography of geographies){
  const subset=records.filter(x=>x.role===role&&x.geography===geography);
  const result=evaluate(subset,{role,geography},now);
  summaries.push({role,geography,rawRows:subset.length,annualSalaryRows:subset.filter(x=>x.basis==='annual'&&x.low>0&&x.high>=x.low).length,
    distinctCityAgencies:new Set(subset.map(x=>x.agency)).size,independentEmployers:subset.length?1:0,
    methodResult:result.decision,eligibleNormalized:result.included,
    sampleIdHashes:subset.slice(0,2).map(x=>x.sampleHash)});
}
const output={source:'NYC Open Data / Jobs NYC Postings',dataset:'kpav-sd4t',retrievedAt:now.toISOString(),
  datasetUrl:'https://data.cityofnewyork.us/City-Government/Jobs-NYC-Postings/kpav-sd4t',
  sampledExternalRows:rows.length,distinctObservedGeographies:geographies,sampledFamilies:families,results:summaries,
  limitations:['NYC-only public-sector employer; agencies are not independent employers.',
    'Borough parsed heuristically from work-location text; unknown geography remains excluded.',
    'Work arrangement and remote eligibility are not explicit; borough location is not remote eligibility.',
    'Posting date is not necessarily update date; 90-day recency requires better source metadata.',
    'Sample capped at 5,000 external rows with no pagination or claim of full coverage.',
    'No raw job text or salary rows retained by this script.']};
console.log(JSON.stringify(output,null,2));

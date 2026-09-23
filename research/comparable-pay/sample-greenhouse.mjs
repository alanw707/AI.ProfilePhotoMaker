// Exploratory, read-only cross-employer probe. No raw posting text or rows are stored.
import {evaluate} from './cohort.mjs';
const boards=['airbnb','asana','databricks','reddit','robinhood','instacart','figma','dropbox','stripe','coinbase','discord','gusto','twilio','cloudflare'];
const families=['software','design','operations'];
const geographies=['San Francisco, CA','New York, NY','Seattle, WA'];
function family(title){
  const t=String(title||'').toLowerCase();
  if(/software engineer|application engineer|software developer|backend engineer|frontend engineer/.test(t))return 'software';
  if(/product design|ux design|user experience design/.test(t))return 'design';
  if(/operations manager|business operations|program manager/.test(t))return 'operations';
  return null;
}
function geography(location){
  const t=String(location||'').toLowerCase();
  if(/remote|multiple|united states|anywhere/.test(t))return null;
  if(/san francisco|bay area/.test(t))return 'San Francisco, CA';
  if(/new york|nyc/.test(t))return 'New York, NY';
  if(/seattle/.test(t))return 'Seattle, WA';
  return null;
}
function extractPay(html){
  const text=String(html||'').replace(/<[^>]*>/g,' ').replace(/&nbsp;|&#160;/gi,' ').replace(/&amp;/gi,'&');
  const matches=[...text.matchAll(/\$\s*([0-9]{2,3}(?:,[0-9]{3})?)\s*(?:-|–|to)\s*\$?\s*([0-9]{2,3}(?:,[0-9]{3})?)/gi)];
  const candidates=matches.map(m=>({low:Number(m[1].replaceAll(',','')),high:Number(m[2].replaceAll(',',''))})).filter(x=>x.low>=20000&&x.high<=1000000&&x.high>=x.low);
  if(candidates.length!==1)return null;
  return candidates[0];
}
const now=new Date(),records=[],boardResults=[];
for(const board of boards){
  try{
    const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),12000);
    const response=await fetch('https://boards-api.greenhouse.io/v1/boards/'+board+'/jobs?content=true',{signal:controller.signal});
    clearTimeout(timer);
    if(!response.ok){boardResults.push({board,status:response.status});continue;}
    const data=await response.json();let parsed=0,matched=0;
    for(const job of data.jobs||[]){
      const role=family(job.title),metro=geography(job.location?.name);
      if(!role||!metro)continue;matched++;
      const pay=extractPay(job.content);if(pay)parsed++;
      records.push({id:board+'-'+job.id,canonicalId:board+'-'+(job.internal_job_id||job.id),employer:board,
        role,geography:metro,level:/\bsenior\b|\bstaff\b/i.test(job.title)?'senior':null,
        employmentType:null,eligible:true,employerDisclosed:!!pay,currency:'USD',basis:pay?'annual':null,
        low:pay?.low||0,high:pay?.high||0,updatedAt:job.updated_at||''});
    }
    boardResults.push({board,status:response.status,totalJobs:data.jobs?.length||0,matchedRoleAndMetro:matched,onePlausibleAnnualRange:parsed});
  }catch(error){boardResults.push({board,error:error.name});}
}
const cohorts=[];
for(const role of families)for(const metro of geographies){
  const subset=records.filter(x=>x.role===role&&x.geography===metro);
  const result=evaluate(subset,{role,geography:metro},now);
  cohorts.push({role,metro,posts:subset.length,employersWithPosts:new Set(subset.map(x=>x.employer)).size,
    parsedPayPosts:subset.filter(x=>x.employerDisclosed).length,eligibleNormalized:result.included,
    normalizedEmployers:result.employers,decision:result.decision,exclusions:result.exclusionReasons});
}
console.log(JSON.stringify({retrievedAt:now.toISOString(),source:'Public Greenhouse Job Board GET endpoints',boards:boardResults,cohorts,limitations:[
  'Public GET access is not a production aggregation license.',
  'Pay extraction is a conservative text heuristic; no structured pay-input confirmation.',
  'Level, employment type, occupation mapping and remote restrictions are not reliably normalized.',
  'Only three named metros and a selected set of employer boards were sampled.',
  'No raw job text, posting rows or observed pay values were stored by this script.'
]},null,2));

// Read-only accessibility probe of a few public employer boards. No postings stored.
const boards=['airbnb','asana','databricks','doordash','reddit','robinhood','instacart','figma','notion','dropbox','stripe','coinbase','openai','discord','gusto','twilio','cloudflare'];
const results=[];
for(const board of boards){
  try{
    const controller=new AbortController();const timeout=setTimeout(()=>controller.abort(),8000);
    const response=await fetch('https://boards-api.greenhouse.io/v1/boards/'+board+'/jobs?content=true',{signal:controller.signal});
    clearTimeout(timeout);
    const data=response.ok?await response.json():null;
    const jobs=data?.jobs||[];
    const salaryText=jobs.filter(job=>/\$\s?\d[\d,]*\s*(?:-|–|to)\s*\$?\s?\d/i.test(job.content||''));
    const remoteText=jobs.filter(job=>/remote/i.test(job.location?.name||''));
    results.push({board,status:response.status,publicPosts:data?.jobs?.length??null,reportedTotal:data?.meta?.total??null,postsWithPayRangeText:salaryText.length,postsWithRemoteInLocation:remoteText.length});
  }catch(error){results.push({board,error:error.name});}
}
console.log(JSON.stringify({retrievedAt:new Date().toISOString(),results},null,2));

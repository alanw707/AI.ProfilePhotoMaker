/* Connected, local-only prototype. All seeded people, places and numbers are illustrative. */
const pages = [
  ['agent','Career agent','M3 10.5 10.5 3 18 10.5 10.5 18 3 10.5Z M10.5 6.5v8 M6.5 10.5h8'],
  ['profile','Career profile','M5 18v-2a5 5 0 0 1 10 0v2 M10 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z'],
  ['analytics','Career analytics','M3 18V9h3v9 M9 18V5h3v13 M15 18v-7h3v7'],
  ['heatmap','Career heatmap','M3 7l5-3 5 3 5-3v13l-5 3-5-3-5 3V7Z M8 4v13 M13 7v13'],
  ['roadmaps','Career roadmaps','M4 17h5v-5h5V7h5 M16 7h3v3 M4 17l3-3'],
  ['materials','Career materials','M5 3h9l4 4v14H5z M14 3v5h4 M8 12h7 M8 16h7']
];
const seeded = {
  page:'agent', name:'Maya Rivera', title:'Operations lead', city:'Denver, CO',
  role:'Operations Manager', arrangement:'Hybrid or remote', weeklyHours:'4 hours', canRelocate:false,
  confirmed:true, profileEdit:false, runState:'complete', selectedMarkets:['Denver, CO'],
  selectedRoute:'closest', completedTasks:[], materialTab:'resume', editedResume:false,
  resume:'Maya Rivera\nOperations Lead\n\nSUMMARY\nOperations leader with 9 years of experience improving cross-team workflows, service delivery, and reporting.\n\nEXPERIENCE\nLed process redesign across three departments. Reduced handoff time by documenting responsibilities and introducing shared reporting.\n\nSKILLS\nProcess improvement · Stakeholder coordination · Reporting',
  summary:'Operations leader focused on making complex work easier to run. Experienced in process improvement, team coordination, and clear reporting.',
  showMap:false, messageHistory:[], assistantOpen:false
};
const storeKey='career-prototype-v1';
const routeScroll=Object.create(null);
if('scrollRestoration' in history)history.scrollRestoration='manual';
let state;
try { state = {...seeded,...JSON.parse(sessionStorage.getItem(storeKey)||'{}')}; } catch { state={...seeded}; }
const $=(sel)=>document.querySelector(sel);
const save=()=>{try{sessionStorage.setItem(storeKey,JSON.stringify(state));}catch{}};
const clean=(s)=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const notify=(msg)=>{ const toast=$('#toast');toast.textContent=msg;toast.hidden=false;clearTimeout(notify.timer);notify.timer=setTimeout(()=>toast.hidden=true,3500);$('#save-status').textContent=msg; };
const action=(name,label,kind='')=>`<button type="button" class="button ${kind}" data-action="${name}">${label}</button>`;
const link=(page,label,kind='')=>`<a class="button ${kind}" href="#${page}">${label}</a>`;
const head=(title,desc,tools='')=>`<header class="page-head"><div><h1>${title}</h1><p>${desc}</p></div>${tools?'<div class="head-actions">'+tools+'</div>':''}</header>`;
const section=(title,body,lead='')=>`<section class="section"><h2>${title}</h2>${lead?'<p class="section-lead">'+lead+'</p>':''}${body}</section>`;
const statusText={empty:'No profile yet',working:'Research in progress',input:'Your answer is needed',partial:'Partial result saved',complete:'Brief ready',stale:'Needs refresh',error:'Source unavailable',quota:'Free limit reached'};
const marketRows=[
  {city:'Denver, CO',pay:'$94k–$141k',signal:'Published wage benchmark',fit:'Current market',value:'mid'},
  {city:'Seattle, WA',pay:'$110k–$165k',signal:'Published wage benchmark',fit:'Relocation required',value:'high'},
  {city:'Minneapolis, MN',pay:'$89k–$134k',signal:'Published wage benchmark',fit:'Relocation required',value:'mid'},
  {city:'Atlanta, GA',pay:'$82k–$123k',signal:'Published wage benchmark',fit:'Relocation required',value:'low'},
  {city:'Boise, ID',pay:'Unavailable',signal:'Suppressed in this example',fit:'Evidence unavailable',value:'unknown'}
];
function renderNav(){
  const markup=pages.map(([id,label,path])=>`<a href="#${id}" class="nav-link" ${state.page===id?'aria-current="page"':''}><span class="nav-icon" aria-hidden="true"><svg viewBox="0 0 21 21"><path d="${path}"/></svg></span>${label}</a>`).join('');
  $('#primary-nav').innerHTML=markup;
  $('#mobile-nav').innerHTML=markup;
}
function renderAssistant(){
  const prompts={
    agent:['What should I do next?','What is still uncertain?'],
    profile:['Which facts need confirmation?','Help me improve my summary'],
    analytics:['Explain this pay range','What would strengthen my case?'],
    heatmap:['Compare these locations','Am I eligible for remote jobs?'],
    roadmaps:['Why this route?','Adjust to my available time'],
    materials:['Check my resume claims','Do I need a profile photo?']
  };
  $('#assistant-context').innerHTML=`Viewing <strong>${pages.find(p=>p[0]===state.page)[1]}</strong> · Target: ${clean(state.role)}<br><span class="small">Responses use only fictional example content.</span>`;
  const intro={
    agent:'Your fictional brief is ready. Compare markets or choose a next action.',
    profile:'Your confirmed experience anchors the analysis. A proposed change needs your review.',
    analytics:'The occupational wage benchmark and comparable pay answer different questions. Check the definitions before comparing.',
    heatmap:'The table contains the same illustrative locations as the map. Unknown data is separate from low values.',
    roadmaps:'You can choose a path and change the weekly effort. Timelines are planning scenarios.',
    materials:'This resume draft uses confirmed example facts. Review every sentence before export.'
  };
  $('#assistant-messages').innerHTML=`<div class="message"><small>Career agent · prototype</small><p>${intro[state.page]}</p></div>`+state.messageHistory.filter(m=>m.page===state.page).map(m=>`<div class="message ${m.mine?'mine':''}"><small>${m.mine?'You':'Career agent · prototype'}</small><p>${clean(m.text)}</p></div>`).join('')+`<div class="button-row">${prompts[state.page].map((p,i)=>`<button class="assist-prompt" type="button" data-action="prompt" data-prompt="${clean(p)}">${p}</button>`).join('')}</div>`;
}
function renderAgent(){
  const run=state.runState;
  const stateChoice=`<div class="state-switch"><label for="demo-state">Preview a state</label><select id="demo-state" aria-label="Preview a task state">${Object.entries(statusText).map(([k,v])=>`<option value="${k}" ${run===k?'selected':''}>${v}</option>`).join('')}</select></div>`;
  let main='';
  if(run==='empty')main=`<div class="paper"><h2>Start with your background</h2><p>You can enter professional facts manually. A future release will accept a resume for review.</p><div class="button-row">${link('profile','Tell me about my background','primary')}${action('demo-confirm','Use the fictional example')}</div></div>`;
  else if(run==='working')main=`<div class="paper"><span class="status warn">Working · step 2 of 3</span><h2>Comparing your role with market evidence</h2><div class="step-track" aria-hidden="true"><span class="done"></span><span class="active"></span><span></span></div><p>The next step is a saved brief. This is a simulated progress state.</p>${action('cancel-run','Cancel example run')}</div>`;
  else if(run==='input')main=`<div class="paper"><span class="status warn">Needs your answer</span><h2>Can you relocate for the right role?</h2><p>We will keep the analysis to eligible markets. Your answer is a simulated preference.</p><div class="button-row">${action('answer-no','No relocation','primary')}${action('answer-yes','Yes, I can relocate')}</div></div>`;
  else if(run==='quota')main=`<div class="paper"><span class="status warn">Free research limit reached</span><h2>Your saved work is still here</h2><p>A real release would show the measured allowance and exact reset time. You can review the fictional brief and export the sample materials now.</p>${link('materials','Open saved materials','primary')}</div>`;
  else if(run==='error')main=`<div class="paper"><span class="status error">Market source unavailable</span><h2>We could not refresh this evidence</h2><p>The saved example remains available. Try again when the source returns.</p><div class="button-row">${action('retry-run','Retry example task','primary')}${link('analytics','Read saved analysis')}</div></div>`;
  else main=`<div class="paper"><span class="status ${run==='partial'||run==='stale'?'warn':''}">${statusText[run]}</span><h2>${run==='stale'?'Your profile changed since this brief':run==='partial'?'One source is missing; your brief is saved':'Your next move, in focus'}</h2><p>Fictional profile: ${clean(state.name)} · ${clean(state.title)}. Target: ${clean(state.role)}. The brief highlights a supported transition and a salary benchmark whose scope is explicit.</p><div class="button-row">${link('analytics','Read the career brief','primary')}${link('heatmap','Compare locations')}${run==='stale'?action('retry-run','Refresh example brief'):''}</div></div>`;
  return head('Make the next move clearer','Your goal, recent work, and one useful next action stay together.',stateChoice)+section('Current goal',`<div class="scenario-banner"><div><h2>${clean(state.role)}</h2><p>${clean(state.city)} · ${clean(state.arrangement)} · ${clean(state.weeklyHours)} available weekly</p></div>${link('profile','Review my profile')}</div>`)+section('Your next action',main)+section('Continue the journey',`<div class="choice-list"><div class="artifact-row"><div><h3>Understand the market</h3><p>Inspect illustrative pay evidence, assumptions, and role fit.</p></div>${link('analytics','Open analytics')}</div><div class="artifact-row"><div><h3>Choose where to focus</h3><p>Compare eligible places in a map and equivalent table.</p></div>${link('heatmap','Explore markets')}</div><div class="artifact-row"><div><h3>Prepare to act</h3><p>Turn a target into steps and usable materials.</p></div>${link('roadmaps','Open roadmap')}</div></div>`);
}
function renderProfile(){
  const edit=state.profileEdit?`<div class="review-change"><span class="label">Proposed profile update</span><h3>Review before accepting</h3><dl><dt>Current</dt><dd>${clean(state.title)}</dd><dt>Proposed</dt><dd class="new">Senior Operations Lead</dd><dt>Source</dt><dd>Fictional resume excerpt · needs confirmation</dd></dl><div class="button-row">${action('accept-profile','Accept change','primary')}${action('dismiss-profile','Dismiss')}</div></div>`:`<div class="note">This fictional profile is confirmed. Try “Review a proposed edit” to see how uncertain resume facts would be handled.</div>`;
  return head('A profile you can trust','Professional facts are inspectable and correctable before they shape advice.',action('propose-profile','Review a proposed edit','primary'))+
  section('Professional snapshot',`<div class="grid-2"><div class="paper"><span class="label">Confirmed background</span><h3 style="margin-top:12px">${clean(state.name)}</h3><p>${clean(state.title)} · 9 years in operations</p><p>Process improvement, coordination, and reporting.</p><span class="status">Added by you · confirmed</span></div><div class="paper"><span class="label">Current direction</span><h3 style="margin-top:12px">${clean(state.role)}</h3><p>Based in ${clean(state.city)}. Prefers ${clean(state.arrangement)} roles. ${clean(state.weeklyHours)} each week for the transition.</p><span class="status muted">Preferences · editable</span></div></div>`)+
  section('Evidence and corrections',edit)+
  section('Your goal',`<form id="goal-form"><div class="form-grid"><div><label class="field" for="goal-role">Target role</label><input id="goal-role" type="text" value="${clean(state.role)}" required maxlength="80"></div><div><label class="field" for="goal-city">Home market</label><input id="goal-city" type="text" value="${clean(state.city)}" required maxlength="80"></div><div><label class="field" for="goal-arrangement">Work arrangement</label><select id="goal-arrangement"><option ${state.arrangement==='Hybrid or remote'?'selected':''}>Hybrid or remote</option><option ${state.arrangement==='On-site'?'selected':''}>On-site</option><option ${state.arrangement==='Remote only'?'selected':''}>Remote only</option></select></div><div><label class="field" for="goal-hours">Time available each week</label><select id="goal-hours"><option ${state.weeklyHours==='4 hours'?'selected':''}>4 hours</option><option ${state.weeklyHours==='2 hours'?'selected':''}>2 hours</option><option ${state.weeklyHours==='8 hours'?'selected':''}>8 hours</option></select></div></div><div class="section-actions"><button class="button primary" type="submit">Save goal</button></div></form>`)+
  section('Resume intake',`<div class="paper"><h3>Bring your own experience</h3><p>The first product release will offer private document upload and a line-by-line confirmation step. This prototype demonstrates the review pattern using fictional facts. No file is sent or stored by this page.</p><span class="status muted">Prototype · upload inactive</span></div>`);
}
function renderAnalytics(){
 const old=state.runState==='stale'?'<span class="status warn">Based on an earlier profile version</span>':'';
 return head('Understand the market, and your fit','Illustrative role analysis. Every number below is fictional and demonstrates how evidence will be labeled.',action('explain-range','Explain this range'))+
 section('Compensation, with definitions',`<div class="comparison"><div><h3>Occupational wage benchmark</h3><div class="metric">$88k–$139k <small>annual wages</small></div><p>Example 25th–75th percentile for Operations Managers in the Denver metro. Illustrative BLS-style measure, not live BLS data.</p><div class="range-track"></div><div class="range-labels"><span>25th percentile</span><span>75th percentile</span></div></div><div><h3>Personalized comparable pay</h3><div class="metric" style="font-size:21px;color:#60716d">Not available yet</div><p>A real range needs a qualified cohort of current employer-disclosed pay for comparable duties, level, location and work arrangement. We will not rename the benchmark as personal pay.</p>${action('explain-range','Why unavailable?','subtle')}</div></div><p class="small">Illustrative sample · no source data connected · annual wage and advertised pay are separate measures. Desired pay is a preference, not evidence. ${old}</p>`)+
 section('What supports this direction',`<div class="paper plain"><ul class="evidence-list"><li><strong>Confirmed</strong><span>Cross-team operations work · fictional profile</span></li><li><strong>Confirmed</strong><span>Workflow redesign and reporting · fictional profile</span></li><li><strong>Needs evidence</strong><span>Budget ownership and scope of people management</span></li><li><strong>Unknown</strong><span>Employer-disclosed comparable pay in the chosen market</span></li></ul></div>`)+
 section('Role directions to compare',`<div class="role-list"><div class="artifact-row"><div><h3>Operations Manager</h3><p>Closest match to the confirmed responsibilities.</p></div><span class="status">Current target</span></div><div class="artifact-row"><div><h3>Program Manager</h3><p>Potential stretch; clarify program ownership first.</p></div>${action('choose-role','Explore role')}</div></div>`)+
 section('Evidence notes',`<div class="note"><strong>What a real report must disclose</strong><p>Dataset and release date, wage definition, occupation code, geography, suppressed cells, comparable cohort, exclusions, and limits. The example figure above is deliberately synthetic.</p></div>`)+
 section('Next decision',`<div class="button-row">${link('heatmap','Compare markets','primary')}${link('roadmaps','Plan toward this role')}</div>`);
}
function renderHeatmap(){
 const options=marketRows.map(m=>`<tr><td data-label="Market"><strong>${m.city}</strong></td><td data-label="Example annual range">${m.pay}</td><td data-label="Constraint">${m.fit==='Relocation required'&&state.canRelocate?'Relocation possible':m.fit}</td><td data-label="Action"><button class="button ${state.selectedMarkets.includes(m.city)?'selected':''}" type="button" data-action="compare-market" data-market="${m.city}" aria-pressed="${state.selectedMarkets.includes(m.city)}">${state.selectedMarkets.includes(m.city)?'Selected':'Compare'}</button></td></tr>`).join('');
 const selected=state.selectedMarkets.join(' · ');
 return head('Find a market that fits your life','Compare places using the same measure, with location and remote restrictions visible.')+
 section('Explore locations',`<div class="note warn">All values and regions shown here are illustrative. The simplified map is a navigation sketch; the table contains the full comparison.</div><div class="map-controls"><div><label class="field" for="market-search">Search location</label><input id="market-search" type="search" placeholder="Try Seattle" autocomplete="off"></div><div><label class="field" for="market-metric">Measure</label><select id="market-metric"><option>Annual wage benchmark</option><option disabled>Observed postings · data needed</option><option disabled>Employment concentration · data needed</option></select></div><div><label class="field" for="market-work">Work arrangement</label><select id="market-work"><option>All arrangements</option><option>Hybrid or remote · eligibility unknown</option></select></div></div><button class="button mobile-map-toggle" type="button" data-action="toggle-map">${state.showMap?'Show list first':'Show map'}</button><div class="map-panel ${state.showMap?'':'collapsed'}" role="img" aria-label="Illustrative map sketch. Use the following table for all values."><svg viewBox="0 0 650 270" aria-hidden="true"><path data-value="high" d="M38 54l62 4 10 55-40 22-29-31z"/><path data-value="mid" d="M115 90l93-20 24 62-36 50-88-13z"/><path data-value="mid" d="M245 80l95-8 14 71-59 27-50-32z"/><path data-value="unknown" d="M355 50l80 22-8 100-70 7z"/><path data-value="low" d="M446 105l82 12 23 53-80 31-41-27z"/><path data-value="high" d="M538 49l79 11-9 113-52 14-23-62z"/><text x="47" y="89">WA</text><text x="146" y="121">CO</text><text x="271" y="118">MN</text><text x="371" y="111">ID</text><text x="478" y="150">GA</text><text x="560" y="106">NY</text></svg></div><div class="map-legend"><span><i class="legend-chip" style="background:#277b70"></i> Higher example</span><span><i class="legend-chip" style="background:#57998a"></i> Middle example</span><span><i class="legend-chip" style="background:#bdd0ba"></i> Lower example</span><span><i class="legend-chip" style="background:#c4cac3"></i> Unavailable</span></div>`)+
 section('Compare locations',`<p class="small">Selected: ${clean(selected)}. Choose up to three. Table values are fictional annual wage examples, not current job offers.</p><div class="table-wrap"><table><thead><tr><th scope="col">Market</th><th scope="col">Example annual range</th><th scope="col">Constraint</th><th scope="col">Action</th></tr></thead><tbody id="market-table">${options}</tbody></table></div><div class="section-actions"><label class="field" for="target-market">Location to save in your goal</label><select id="target-market">${state.selectedMarkets.map(m=>`<option>${clean(m)}</option>`).join('')}</select><div class="button-row" style="margin-top:10px">${action('save-market','Save target market','primary')}</div></div>`)+
 section('Remote eligibility',`<div class="note">“Remote” on a listing does not mean eligible from every U.S. location. This prototype has no live listings, so remote eligibility remains unknown.</div>`);
}
function renderRoadmaps(){
 const routes=[['closest','Closest fit','Build on existing operations leadership.','Clarify scope and prepare targeted examples.'],['stretch','Higher ambition','Explore program leadership after documenting cross-team ownership.','More evidence and interview preparation needed.'],['steady','Steadier transition','Keep the current role while testing adjacent opportunities.','Lower weekly effort; a longer timeline.']];
 const selected=routes.find(r=>r[0]===state.selectedRoute);
 const tasks=[['fact','Document one measurable process improvement','This week · 30 minutes'],['resume','Tailor the resume to the target role','Within 30 days · 2 hours'],['market','Compare two eligible markets','Within 60 days · 1 hour'],['interview','Prepare two interview stories','Within 90 days · 2 hours']];
 return head('Turn a direction into action','Choose a practical route. Change the effort assumptions and keep your own progress.',link('materials','Open materials'))+
 section('Choose your path',`<div class="grid-3">${routes.map(r=>`<div class="route-choice ${state.selectedRoute===r[0]?'active':''}"><span class="status ${state.selectedRoute===r[0]?'':'muted'}">${state.selectedRoute===r[0]?'Selected route':'Option'}</span><h3>${r[1]}</h3><p>${r[2]}</p><p class="small">${r[3]}</p><button type="button" class="button ${state.selectedRoute===r[0]?'selected':''}" data-action="select-route" data-route="${r[0]}" aria-pressed="${state.selectedRoute===r[0]}">${state.selectedRoute===r[0]?'Selected':'Choose route'}</button></div>`).join('')}</div>`)+
 section('Your plan',`<div class="scenario-banner"><div><h2>${selected[1]} · ${clean(state.role)}</h2><p>${clean(state.weeklyHours)} available each week. Dates are editable planning assumptions, never promised outcomes.</p></div>${action('adjust-effort','Adjust time')}</div><div class="paper" style="margin-top:16px">${tasks.map(([id,label,when])=>`<div class="task-row"><input type="checkbox" id="task-${id}" data-task="${id}" ${state.completedTasks.includes(id)?'checked':''}><label for="task-${id}">${label}<small>${when}</small></label></div>`).join('')}</div>`)+
 section('Why these steps?',`<ul class="evidence-list"><li><strong>Confirmed</strong><span>Process improvement work can support the closest-fit route.</span></li><li><strong>Needs evidence</strong><span>Budget and people-management scope may matter for the higher-ambition route.</span></li><li><strong>Trade-off</strong><span>Changing weekly effort changes the timing assumption, not the pay estimate.</span></li></ul>`)+
 section('Continue',link('materials','Prepare a factual resume','primary'));
}
function renderMaterials(){
 const tabs=[['resume','Resume'],['summary','Professional summary'],['photos','Photos']];
 const tabMarkup=`<div class="material-tabs" role="tablist" aria-label="Career materials">${tabs.map(([id,label])=>`<button type="button" role="tab" data-action="material-tab" data-tab="${id}" aria-selected="${state.materialTab===id}" tabindex="${state.materialTab===id?'0':'-1'}">${label}</button>`).join('')}</div>`;
 let content='';
 if(state.materialTab==='photos')content=`<div class="paper"><h3>Professional photos are optional</h3><p>Use a photo you own, continue in the existing photo workspace, or skip this step. The basic career resume export does not include a headshot.</p><div class="button-row">${action('photo-demo','Show photo handoff','primary')}${link('agent','Continue without photos')}</div><p class="small">Prototype only: no package allowances or prices are simulated here. Actual generation must use the existing authenticated photo workspace and purchase controls.</p></div>`;
 else content=`<div class="paper"><div class="artifact-row"><div><h3>${state.materialTab==='resume'?'Targeted resume':'Professional summary'}</h3><p>Draft for ${clean(state.role)} · ${state.editedResume?'Edited in this prototype':'Example draft'}</p></div><span class="status muted">Review required</span></div><label class="field" for="material-editor">Edit this draft</label><textarea id="material-editor" class="material-editor">${clean(state.materialTab==='resume'?state.resume:state.summary)}</textarea><div class="button-row section-actions">${action('save-material','Save example edit','primary')}${action('download-material','Download plain-text sample')}</div><p class="small">The prototype downloads text only. Basic PDF/DOCX exports belong to the implementation ticket for career materials.</p></div>`;
 return head('Materials for your next move','Keep drafts tied to the target role. Review what the agent proposes before using it.')+
 section('Your application kit',tabMarkup+`<div style="margin-top:17px">${content}</div>`)+
 section('Source facts',`<ul class="evidence-list"><li><strong>From profile</strong><span>Operations leadership, process redesign, and reporting.</span></li><li><strong>Needs review</strong><span>Add measured outcomes only if you can verify them.</span></li><li><strong>Photo</strong><span>Separate from the resume by default; optional for other profile uses.</span></li></ul>`);
}
const renderers={agent:renderAgent,profile:renderProfile,analytics:renderAnalytics,heatmap:renderHeatmap,roadmaps:renderRoadmaps,materials:renderMaterials};
function render(){
  state.page=pages.some(p=>p[0]===location.hash.slice(1))?location.hash.slice(1):'agent';
  renderNav();$('#context-label').textContent=`Career goal / ${state.role}`;
  $('#view').innerHTML=renderers[state.page]();
  renderAssistant();save();
  document.title=`${pages.find(p=>p[0]===state.page)[1]} · AI Profile Photo Maker prototype`;
}
function navigate(){
  const previous=state.page;
  routeScroll[previous]=window.scrollY;
  render();
  $('#mobile-nav').hidden=true;
  $('#mobile-menu').setAttribute('aria-expanded','false');
  if(previous!==state.page){
    $('#workspace').focus({preventScroll:true});
    window.scrollTo({top:routeScroll[state.page]??0,behavior:'instant'});
  }
}
window.addEventListener('hashchange',navigate);
document.addEventListener('click',event=>{
  const trigger=event.target.closest('[data-action]');if(!trigger)return;
  const a=trigger.dataset.action;
  if(a==='propose-profile'){state.profileEdit=true;render();notify('Proposed edit is ready for review.');}
  if(a==='accept-profile'){state.title='Senior Operations Lead';state.profileEdit=false;state.runState='stale';render();notify('Fact confirmed; earlier brief marked outdated.');}
  if(a==='dismiss-profile'){state.profileEdit=false;render();notify('Proposed change dismissed.');}
  if(a==='demo-confirm'){state.runState='complete';render();}
  if(a==='retry-run'){state.runState='working';render();notify('Example task started. Select “Brief ready” to preview completion.');}
  if(a==='cancel-run'){state.runState='partial';render();notify('Example task cancelled; saved work remains available.');}
  if(a==='answer-no'||a==='answer-yes'){state.canRelocate=a==='answer-yes';state.runState='complete';render();notify('Example answer recorded; brief ready.');}
  if(a==='choose-role'){state.role='Program Manager';state.runState='stale';render();notify('Target changed; earlier analysis needs a refresh.');}
  if(a==='explain-range'){openAssistant();sendPrompt('Explain this pay range');}
  if(a==='compare-market'){const m=trigger.dataset.market;const ix=state.selectedMarkets.indexOf(m);if(ix>=0)state.selectedMarkets.splice(ix,1);else if(state.selectedMarkets.length<3)state.selectedMarkets.push(m);else return notify('You can compare up to three places. Remove one first.');render();notify('Comparison updated.');}
  if(a==='save-market'){state.city=$('#target-market')?.value||state.city;state.runState='stale';render();notify('Goal location updated; earlier brief marked outdated.');}
  if(a==='toggle-map'){state.showMap=!state.showMap;render();}
  if(a==='select-route'){state.selectedRoute=trigger.dataset.route;render();notify('Roadmap route selected.');}
  if(a==='adjust-effort'){state.weeklyHours=state.weeklyHours==='4 hours'?'2 hours':state.weeklyHours==='2 hours'?'8 hours':'4 hours';render();notify('Weekly effort assumption updated.');}
  if(a==='material-tab'){state.materialTab=trigger.dataset.tab;render();const next=document.querySelector(`[data-tab="${state.materialTab}"]`);next?.focus();}
  if(a==='save-material'){state[state.materialTab==='resume'?'resume':'summary']=$('#material-editor').value;state.editedResume=true;save();notify('Example draft saved in this browser session.');}
  if(a==='download-material'){const content=$('#material-editor').value;const blob=new Blob([content],{type:'text/plain;charset=utf-8'});const url=URL.createObjectURL(blob);const download=document.createElement('a');download.href=url;download.download=state.materialTab==='resume'?'sample-resume.txt':'sample-summary.txt';download.click();setTimeout(()=>URL.revokeObjectURL(url),1000);notify('Plain-text sample downloaded.');}
  if(a==='photo-demo'){notify('Photo handoff previewed. The real workflow will show your authenticated allowance before generation.');}
  if(a==='photo-jump'){state.materialTab='photos';save();}
  if(a==='show-help'){notify('Choose any page. Use the state selector on Career agent to preview incomplete and error states. All data is fictional.');}
  if(a==='prompt'){openAssistant();sendPrompt(trigger.dataset.prompt);}
});
document.addEventListener('change',event=>{
 if(event.target.id==='demo-state'){state.runState=event.target.value;render();}
 if(event.target.matches('[data-task]')){const id=event.target.dataset.task;if(event.target.checked&&!state.completedTasks.includes(id))state.completedTasks.push(id);if(!event.target.checked)state.completedTasks=state.completedTasks.filter(x=>x!==id);save();notify('Roadmap progress updated.');}
});
document.addEventListener('submit',event=>{
 if(event.target.id!=='goal-form')return;event.preventDefault();
 state.role=$('#goal-role').value.trim()||state.role;state.city=$('#goal-city').value.trim()||state.city;state.arrangement=$('#goal-arrangement').value;state.weeklyHours=$('#goal-hours').value;state.runState='stale';render();notify('Goal saved; existing report needs a refresh.');
});
document.addEventListener('input',event=>{
 if(event.target.id==='market-search'){const q=event.target.value.toLowerCase();document.querySelectorAll('#market-table tr').forEach(row=>row.hidden=!row.textContent.toLowerCase().includes(q));}
});
function openAssistant(){state.assistantOpen=true;$('#assistant').classList.add('open');$('#mobile-agent').setAttribute('aria-expanded','true');$('#assistant-input').focus();save();}
function closeAssistant(){state.assistantOpen=false;$('#assistant').classList.remove('open');$('#mobile-agent').setAttribute('aria-expanded','false');$('#mobile-agent').focus();save();}
function sendPrompt(prompt){
 const q=(prompt||$('#assistant-input').value).trim();if(!q)return;
 const answer=q.toLowerCase().includes('range')||q.toLowerCase().includes('pay')?'The benchmark is a fictional occupation-wide wage example. A personalized range is unavailable until current, licensed and comparable employer pay observations are qualified. It is not a prediction of your offer.':q.toLowerCase().includes('remote')?'Remote eligibility depends on each posting’s actual state or country restrictions. No live listings are connected to this prototype.':q.toLowerCase().includes('photo')?'Photos are optional. The career resume omits a headshot by default, and any paid photo action stays under your control.':'For this example, review the source facts on this page and choose the next action in the workspace. This is a scripted prototype response, not AI research.';
 state.messageHistory.push({page:state.page,mine:true,text:q},{page:state.page,mine:false,text:answer});$('#assistant-input').value='';renderAssistant();$('#assistant-messages').scrollTop=$('#assistant-messages').scrollHeight;save();
}
$('#assistant-send').addEventListener('click',()=>sendPrompt());
$('#assistant-input').addEventListener('keydown',event=>{if(event.key==='Enter')sendPrompt();});
$('#mobile-agent').addEventListener('click',openAssistant);
$('#assistant-close').addEventListener('click',closeAssistant);
$('#mobile-menu').addEventListener('click',()=>{const nav=$('#mobile-nav');nav.hidden=!nav.hidden;$('#mobile-menu').setAttribute('aria-expanded',String(!nav.hidden));});
document.addEventListener('keydown',event=>{if(event.key==='Escape'){if(state.assistantOpen)closeAssistant();else if(!$('#mobile-nav').hidden){$('#mobile-nav').hidden=true;$('#mobile-menu').setAttribute('aria-expanded','false');$('#mobile-menu').focus();}}});
render();

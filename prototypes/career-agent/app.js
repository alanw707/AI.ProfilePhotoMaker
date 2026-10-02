/* Connected, local-only prototype. All seeded people, places and numbers are illustrative. */
// Nav glyphs share one 24x24 grid, each centred on (12,12) so the rail reads evenly.
const pages = [
  ['agent','Career agent','M12 4l2.2 5.8L20 12l-5.8 2.2L12 20l-2.2-5.8L4 12l5.8-2.2Z'],
  ['profile','Career profile','M12 12a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z M5.5 19a6.5 6.5 0 0 1 13 0'],
  ['analytics','Career analytics','M4.5 19.5v-8 M9.5 19.5v-15 M14.5 19.5v-11 M19.5 19.5v-7'],
  ['heatmap','Market comparison','M4 17.5l5-6 4 3 7-8 M15 6.5h5v5'],
  ['roadmaps','Career roadmaps','M4 18h5v-4.5h5V9h6 M17 6l3 3-3 3'],
  ['materials','Career materials','M6 3.5h8l4 4v13H6Z M14 3.5v4h4 M9 12.5h6 M9 16h6']
];
const seeded = {
  page:'agent', name:'Maya Rivera', title:'Operations lead', city:'Denver, CO', targetMarket:'', marketToSave:'', marketStep:1, marketSaved:false, previousTarget:'',
  role:'Operations Manager', arrangement:'Hybrid or remote', weeklyHours:'4 hours', canRelocate:false,
  confirmed:true, profileEdit:false, runState:'complete', selectedMarkets:[],
  selectedRoute:'closest', completedTasks:[], materialTab:'resume', editedResume:false,
  resume:'Maya Rivera\nOperations Lead\n\nSUMMARY\nOperations leader with 9 years of experience improving cross-team workflows, service delivery, and reporting.\n\nEXPERIENCE\nLed process redesign across three departments. Reduced handoff time by documenting responsibilities and introducing shared reporting.\n\nSKILLS\nProcess improvement · Stakeholder coordination · Reporting',
  summary:'Operations leader focused on making complex work easier to run. Experienced in process improvement, team coordination, and clear reporting.',
  marketQuery:'', messageHistory:[], assistantOpen:false
};
// v1 saved the chosen destination over the home city; v2 used a table instead of
// the step-by-step market wizard. Reset older fictional state.
const storeKey='career-prototype-v3';
const routeScroll=Object.create(null);
if('scrollRestoration' in history)history.scrollRestoration='manual';
let state;
try { state = {...seeded,...JSON.parse(sessionStorage.getItem(storeKey)||'{}')}; } catch { state={...seeded}; }
const $=(sel)=>document.querySelector(sel);
function currentTheme(){return document.documentElement.dataset.theme==='dark'?'dark':'light';}
function syncThemeControls(){
  const dark=currentTheme()==='dark';
  document.querySelectorAll('.theme-toggle').forEach(b=>{
    b.setAttribute('aria-label',dark?'Switch to light theme':'Switch to dark theme');
    b.removeAttribute('aria-pressed');
    const word=b.querySelector('.theme-word');if(word)word.textContent=dark?'Light':'Dark';
  });
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content',dark?'#0d1011':'#eef1ef');
}
function setTheme(theme){
  document.documentElement.dataset.theme=theme;
  try{localStorage.setItem('career-theme',theme);}catch{}
  syncThemeControls();
}
const save=()=>{try{sessionStorage.setItem(storeKey,JSON.stringify(state));}catch{}};
const clean=(s)=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const notify=(msg)=>{ const toast=$('#toast');toast.textContent=msg;toast.hidden=false;clearTimeout(notify.timer);notify.timer=setTimeout(()=>toast.hidden=true,3500);$('#save-status').textContent=msg; };
const action=(name,label,kind='')=>`<button type="button" class="button ${kind}" data-action="${name}">${label}</button>`;
const link=(page,label,kind='')=>`<a class="button ${kind}" href="#${page}">${label}</a>`;
const head=(title,desc,tools='')=>`<header class="page-head"><div><h1>${title}</h1><p>${desc}</p></div>${tools?'<div class="head-actions">'+tools+'</div>':''}</header>`;
const section=(title,body,lead='')=>`<section class="section"><h2>${title}</h2>${lead?'<p class="section-lead">'+lead+'</p>':''}${body}</section>`;
const statusText={empty:'No profile yet',working:'Research in progress',input:'Your answer is needed',partial:'Partial result saved',complete:'Brief ready',stale:'Needs refresh',error:'Source unavailable',quota:'Free limit reached'};
const marketRows=[
  {city:'Denver, CO',pay:'$94k–$141k',low:94,high:141},
  {city:'Seattle, WA',pay:'$110k–$165k',low:110,high:165},
  {city:'Minneapolis, MN',pay:'$89k–$134k',low:89,high:134},
  {city:'Atlanta, GA',pay:'$82k–$123k',low:82,high:123},
  {city:'Boise, ID',pay:'Unavailable'}
];
function intervalPlot(rows,compact=false,showSelection=false){
  const interval=(m)=>m.low==null?'<span class="interval-unavailable">No example value</span>':`<span class="interval-band" style="--interval-start:${((m.low-80)/90*100).toFixed(1)}%;--interval-end:${((m.high-80)/90*100).toFixed(1)}%"></span>`;
  return `<figure class="interval-chart ${compact?'compact':''}" aria-label="Illustrative annual occupational wage intervals, not live BLS data"><figcaption><strong>${compact?'Market glimpse':'Compare wage intervals'}</strong><span>Fictional 25th–75th percentile examples · annual wages</span></figcaption><div class="interval-axis" aria-hidden="true"><span>$80k</span><span>$110k</span><span>$140k</span><span>$170k</span></div><div class="interval-rows">${rows.map(m=>`<div class="interval-row ${showSelection&&state.selectedMarkets.includes(m.city)?'selected':''} ${showSelection&&m.city===state.city?'home':''}" data-market="${clean(m.city)}"><span class="interval-place">${clean(m.city)}${showSelection&&m.city===state.city?' <span class="interval-tag">Home</span>':''}</span><span class="interval-track" aria-hidden="true">${interval(m)}</span><span class="interval-value">${clean(m.pay)}</span></div>`).join('')}</div><p class="interval-footnote">Synthetic illustration, not a personal salary estimate or current job offer.</p></figure>`;
}
function renderNav(){
  const markup=pages.map(([id,label,path])=>`<a href="#${id}" class="nav-link" ${state.page===id?'aria-current="page"':''}><span class="nav-icon" aria-hidden="true"><svg viewBox="0 0 24 24"><path d="${path}"/></svg></span>${label}</a>`).join('');
  const primary=$('#primary-nav');
  if(!primary.querySelector('.nav-active-indicator')){
    primary.innerHTML=`<span class="nav-active-indicator" aria-hidden="true"></span>${markup}`;
    requestAnimationFrame(()=>primary.classList.add('nav-ready'));
  }else primary.querySelectorAll('.nav-link').forEach((item,index)=>{
    if(pages[index][0]===state.page)item.setAttribute('aria-current','page');
    else item.removeAttribute('aria-current');
  });
  placeNavIndicator();
  $('#mobile-nav').innerHTML=markup;
}
/** Size and place the active plate from the real link box; labels wrap, so heights vary. */
function placeNavIndicator(){
  const primary=$('#primary-nav');
  const active=primary?.querySelector('.nav-link[aria-current="page"]');
  if(!active||!active.offsetHeight)return;
  primary.style.setProperty('--indicator-y',`${active.offsetTop}px`);
  primary.style.setProperty('--indicator-h',`${active.offsetHeight}px`);
}
window.addEventListener('resize',()=>requestAnimationFrame(placeNavIndicator));
document.fonts?.ready.then(placeNavIndicator);
function renderAssistant(){
  const prompts={
    agent:['What should I do next?','What is still uncertain?'],
    profile:['Which facts need confirmation?','Help me improve my summary'],
    analytics:['Explain this pay range','What does evidence strength mean?','What would strengthen my case?'],
    heatmap:['Compare these locations','Am I eligible for remote jobs?'],
    roadmaps:['Why this route?','Adjust to my available time'],
    materials:['Check my resume claims','Do I need a profile photo?']
  };
  $('#assistant-context').innerHTML=`Viewing <strong>${pages.find(p=>p[0]===state.page)[1]}</strong> · Target: ${clean(state.role)}<br><span class="small">Responses use only fictional example content.</span>`;
  const intro={
    agent:'Your fictional brief is ready. Compare markets or choose a next action.',
    profile:'Your confirmed experience anchors the analysis. A proposed change needs your review.',
    analytics:'Every chart here uses fictional example data. The wage tiles describe a whole occupation in one area, not your pay. Ask me what any chart means.',
    heatmap:'Four steps: confirm where you live, choose up to three places, compare them side by side, then save one as your target. The wage chart is fictional and remote eligibility is unknown.',
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
  else if(run==='input')main=`<div class="paper"><span class="status warn">Needs your answer</span><h2>Can you relocate for the right role?</h2><p>Your answer changes the on-site relocation labels in the market comparison. It does not establish remote eligibility or run live research.</p><div class="button-row">${action('answer-no','No relocation','primary')}${action('answer-yes','Yes, I can relocate')}</div></div>`;
  else if(run==='quota')main=`<div class="paper"><span class="status warn">Free research limit reached</span><h2>Your saved work is still here</h2><p>A real release would show the measured allowance and exact reset time. You can review the fictional brief and export the sample materials now.</p>${link('materials','Open saved materials','primary')}</div>`;
  else if(run==='error')main=`<div class="paper"><span class="status error">Market source unavailable</span><h2>We could not refresh this evidence</h2><p>The saved example remains available. Try again when the source returns.</p><div class="button-row">${action('retry-run','Retry example task','primary')}${link('analytics','Read saved analysis')}</div></div>`;
  else main=`<div class="paper"><span class="status ${run==='partial'||run==='stale'?'warn':''}">${statusText[run]}</span><h2>${run==='stale'?'Your goal changed since this brief':run==='partial'?'One source is missing; your brief is saved':'Your next move, in focus'}</h2><p>Fictional profile: ${clean(state.name)} · ${clean(state.title)}. Target: ${clean(state.role)}. The brief highlights a supported transition and a salary benchmark whose scope is explicit.</p><div class="button-row">${link('analytics','Read the career brief','primary')}${link('heatmap','Compare locations')}${run==='stale'?action('retry-run','Refresh example brief'):''}</div></div>`;
  const goal=`<div class="goal-overview"><div class="scenario-banner"><div><h2>${clean(state.role)}</h2><p>Home: ${clean(state.city)} · Target market: ${clean(state.targetMarket||'Not selected')} · ${clean(state.arrangement)} · ${clean(state.weeklyHours)} available weekly</p></div>${link('profile','Review my profile')}</div>${intervalPlot(marketRows.slice(0,2),true)}</div>`;
  return head('Make the next move clearer','Your goal, recent work, and one useful next action stay together.',stateChoice)+section('Current goal',goal)+section('Your next action',main)+section('Continue the journey',`<div class="choice-list"><div class="artifact-row"><div><h3>Understand the market</h3><p>Inspect illustrative pay evidence, assumptions, and role fit.</p></div>${link('analytics','Open analytics')}</div><div class="artifact-row"><div><h3>Choose where to focus</h3><p>Compare places using the fictional wage chart and searchable table; remote eligibility is unknown.</p></div>${link('heatmap','Explore markets')}</div><div class="artifact-row"><div><h3>Prepare to act</h3><p>Turn a target into steps and usable materials.</p></div>${link('roadmaps','Open roadmap')}</div></div>`);
}
function renderProfile(){
  const edit=state.profileEdit?`<div class="review-change"><span class="label">Proposed profile update</span><h3>Review before accepting</h3><dl><dt>Current</dt><dd>${clean(state.title)}</dd><dt>Proposed</dt><dd class="new">Senior Operations Lead</dd><dt>Source</dt><dd>Fictional resume excerpt · needs confirmation</dd></dl><div class="button-row">${action('accept-profile','Accept change','primary')}${action('dismiss-profile','Dismiss')}</div></div>`:`<div class="note">This fictional profile is confirmed. Try “Review a proposed edit” to see how uncertain resume facts would be handled.</div>`;
  return head('A profile you can trust','Professional facts are inspectable and correctable before they shape advice.',action('propose-profile','Review a proposed edit','primary'))+
  section('Professional snapshot',`<div class="grid-2"><div class="paper"><span class="label">Confirmed background</span><h3 style="margin-top:12px">${clean(state.name)}</h3><p>${clean(state.title)} · 9 years in operations</p><p>Process improvement, coordination, and reporting.</p><span class="status">Added by you · confirmed</span></div><div class="paper"><span class="label">Current direction</span><h3 style="margin-top:12px">${clean(state.role)}</h3><p>Home market: ${clean(state.city)}. Target market: ${clean(state.targetMarket||'Not selected')}. Prefers ${clean(state.arrangement)} roles. ${clean(state.weeklyHours)} each week for the transition.</p><span class="status muted">Preferences · editable</span></div></div>`)+
  section('Evidence and corrections',edit)+
  section('Your goal',`<form id="goal-form"><div class="form-grid"><div><label class="field" for="goal-role">Target role</label><input id="goal-role" type="text" value="${clean(state.role)}" required maxlength="80"></div><div><label class="field" for="goal-city">Home market</label><input id="goal-city" type="text" value="${clean(state.city)}" required maxlength="80"></div><div><label class="field" for="goal-arrangement">Work arrangement</label><select id="goal-arrangement"><option ${state.arrangement==='Hybrid or remote'?'selected':''}>Hybrid or remote</option><option ${state.arrangement==='On-site'?'selected':''}>On-site</option><option ${state.arrangement==='Remote only'?'selected':''}>Remote only</option></select></div><div><label class="field" for="goal-hours">Time available each week</label><select id="goal-hours"><option ${state.weeklyHours==='4 hours'?'selected':''}>4 hours</option><option ${state.weeklyHours==='2 hours'?'selected':''}>2 hours</option><option ${state.weeklyHours==='8 hours'?'selected':''}>8 hours</option></select></div></div><div class="section-actions"><button class="button primary" type="submit">Save goal</button></div></form>`)+
  section('Resume intake',`<div class="paper"><h3>Bring your own experience</h3><p>The first product release will offer private document upload and a line-by-line confirmation step. This prototype demonstrates the review pattern using fictional facts. No file is sent or stored by this page.</p><span class="status muted">Prototype · upload inactive</span></div>`);
}
/* ---------- Career analytics dashboard (all values fictional) ---------- */
const ICON={
  cert:'<path d="M12 3l2.4 1.8 3-.2.9 2.8 2.4 1.8-1 2.8 1 2.8-2.4 1.8-.9 2.8-3-.2L12 21l-2.4-1.8-3 .2-.9-2.8L3.3 14.8l1-2.8-1-2.8 2.4-1.8.9-2.8 3 .2Z"/><path d="M9 12l2 2 4-4"/>',
  edu:'<path d="M3 9l9-5 9 5-9 5Z"/><path d="M7 11.5V16c3 2 7 2 10 0v-4.5"/>',
  work:'<rect x="3" y="7" width="18" height="13" rx="2"/><path d="M9 7V5h6v2M3 13h18"/>',
  doc:'<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5M9 13h7M9 17h5"/>',
  people:'<circle cx="9" cy="8" r="3"/><path d="M3 20a6 6 0 0 1 12 0"/><path d="M16 5a3 3 0 0 1 0 6M18 20a5 5 0 0 0-2-4"/>',
  strength:'<path d="M4 15c2-1 3-4 3-7l3-4 2 2-1 4h6a2 2 0 0 1 2 2l-1 6a2 2 0 0 1-2 2H9l-5-2Z"/>',
  path:'<path d="M3 17l6-6 4 4 8-8"/><path d="M15 7h6v6"/>',
  hurdle:'<path d="M4 20V6M20 20V6M4 9h16M4 15h16"/><path d="M8 9l4 6M12 9l4 6"/>',
  info:'<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8h.01"/>'
};
const svgIcon=(name)=>`<svg viewBox="0 0 24 24" aria-hidden="true">${ICON[name]}</svg>`;
const more=(text,lines=3)=>`<div class="more" style="--lines:${lines}"><p class="more-text">${text}</p><button type="button" class="more-toggle" data-action="toggle-more" aria-expanded="false">Show more</button></div>`;
const panel=(title,sub,body,extra='')=>`<section class="dash-panel" aria-labelledby="${title.toLowerCase().replace(/[^a-z]+/g,'-')}"><header><div><h2 id="${title.toLowerCase().replace(/[^a-z]+/g,'-')}">${title}</h2>${sub?`<p class="panel-sub">${sub}</p>`:''}</div>${extra||'<span class="source-tag">Fictional example</span>'}</header>${body}</section>`;

function evidenceGauge(){
  const levels=['Strong','Supported','Building','Early'];
  const current='Supported';
  return `<div class="gauge"><div class="gauge-levels" aria-hidden="true">${levels.map(l=>`<span class="${l===current?'on':''}">${l}</span>`).join('')}</div><div class="gauge-track" aria-hidden="true"><span class="gauge-fill" style="--fill:62%"></span></div><div class="gauge-card"><div class="gauge-badge"><small>Evidence for ${clean(state.role)}</small><strong>${current}</strong></div>${more(`6 of 9 common requirements for this role have confirmed evidence in the fictional profile: process redesign, cross-team coordination and reporting are strong. Budget ownership and people management have no confirmed examples yet, so the evidence level stays at Supported rather than Strong. This is not a score of the person.`,4)}</div></div>`;
}
function completenessRing(){
  const parts=[['Experience',90,'--c-you'],['Skills',75,'--c5'],['Projects',40,'--c6'],['Education',60,'--c3']];
  const avg=Math.round(parts.reduce((a,p)=>a+p[1],0)/parts.length);
  const arcs=parts.map(([,v,c],i)=>{const r=84-i*14,len=+(2*Math.PI*r).toFixed(1);return `<circle class="ring-bg" cx="100" cy="100" r="${r}" stroke-width="9"/><circle class="ring-arc" cx="100" cy="100" r="${r}" stroke-width="9" stroke="var(${c})" stroke-dasharray="${len}" stroke-dashoffset="${(len*(1-v/100)).toFixed(1)}" style="--len:${len}"/>`;}).join('');
  return `<div class="ring-wrap"><svg viewBox="0 0 200 200" role="img" aria-label="Profile ${avg}% complete. ${parts.map(p=>`${p[0]} ${p[1]}%`).join(', ')}.">${arcs}<text class="ring-center" x="100" y="104" text-anchor="middle">${avg}%</text><text class="ring-caption" x="100" y="121" text-anchor="middle">COMPLETE</text></svg><ul class="chart-legend">${parts.map(([n,v,c])=>`<li><span class="dot" style="--dot:var(${c})"></span>${n} <b>${v}%</b></li>`).join('')}</ul></div>`;
}
function skillsRadar(){
  const axes=['Process improvement','Coordination','Reporting','Budget ownership','People leadership','Vendor management'];
  const asks=[4,4,3,4,4,3], you=[5,4,4,2,2,3];
  const pt=(i,v,r=70)=>{const a=-Math.PI/2+i*2*Math.PI/axes.length;return [100+Math.cos(a)*r*v/5,100+Math.sin(a)*r*v/5];};
  const poly=vals=>vals.map((v,i)=>pt(i,v).map(n=>n.toFixed(1)).join(',')).join(' ');
  const rings=[1,2,3,4,5].map(l=>`<polygon class="radar-grid" points="${poly(axes.map(()=>l))}"/>`).join('');
  const spokes=axes.map((_,i)=>{const [x,y]=pt(i,5);return `<line class="radar-axis" x1="100" y1="100" x2="${x.toFixed(1)}" y2="${y.toFixed(1)}"/>`;}).join('');
  const labels=axes.map((n,i)=>{const [x,y]=pt(i,5,88);const anchor=Math.abs(x-100)<6?'middle':x>100?'start':'end';return `<text class="radar-label" x="${x.toFixed(1)}" y="${(y+3).toFixed(1)}" text-anchor="${anchor}">${n}</text>`;}).join('');
  return `<div class="radar-wrap"><svg viewBox="-58 -10 316 220" aria-hidden="true">${rings}${spokes}<polygon class="radar-asks" points="${poly(asks)}"/><polygon class="radar-you" points="${poly(you)}"/>${labels}</svg><ul class="chart-legend"><li><span class="dot" style="--dot:var(--c2)"></span>What the role usually asks</li><li><span class="dot" style="--dot:var(--c-you)"></span>Your confirmed evidence</li></ul><div class="visually-hidden"><table><caption>Skills compared with the target role, 0 to 5</caption><thead><tr><th scope="col">Skill</th><th scope="col">Role asks</th><th scope="col">You, confirmed</th></tr></thead><tbody>${axes.map((n,i)=>`<tr><th scope="row">${n}</th><td>${asks[i]}</td><td>${you[i]}</td></tr>`).join('')}</tbody></table></div></div>`;
}
function actionMatrix(){
  const items=[
    ['doc','Portfolio','--c1','Write up the three-department process redesign',2,'~2 weeks','A short case study with the before and after turns your strongest confirmed work into evidence an employer can read. Use only outcomes you can verify.'],
    ['work','Work experience','--c-you','Take ownership of a budget line in your current role',4,'1–2 quarters','Budget ownership is the largest gap between this profile and typical Operations Manager postings. Even a small, clearly owned budget changes the evidence level.'],
    ['people','People leadership','--c4','Lead a cross-team project with named owners',4,'~3 months','Leading people you do not manage directly is a credible first step toward the people-management requirement. Record scope, team size and result.'],
    ['cert','Certification','--c2','Lean Six Sigma Green Belt',3,'~8 weeks','Gives a recognized name to process-improvement work you already do. Check whether postings in your target market actually mention it before paying for a course.'],
    ['edu','Education','--c3','Short course in operations finance',3,'~6 weeks','Helps you speak to cost and budget questions in interviews while you build real budget experience.']
  ];
  return `<div class="matrix" id="action-matrix" role="region" aria-label="Five actions you could take" tabindex="0">${items.map(([ic,cat,c,title,effort,time,why])=>`<article class="matrix-card"><span class="chip" style="--chip:var(${c})">${svgIcon(ic)}${cat}</span><h3>${title}</h3><dl class="matrix-facts"><div><dt>Time to show evidence</dt><dd><span class="time-pill">${time}</span></dd></div><div><dt>Effort</dt><dd><span class="effort" role="img" aria-label="Effort ${effort} of 5">${[1,2,3,4,5].map(n=>`<i class="${n<=effort?'on':''}"></i>`).join('')}</span></dd></div></dl>${more(why,2)}</article>`).join('')}</div><button type="button" class="button matrix-more" data-action="matrix-all" aria-controls="action-matrix" aria-expanded="false">Show all 5 actions</button>`;
}
function industryBars(){
  const rows=[['Logistics',78,'--c1'],['Healthcare',64,'--c-you'],['Manufacturing',55,'--c2'],['Government',41,'--c4'],['Retail',33,'--c5'],['Education',18,'--c3']];
  return `<div class="bars-wrap"><dl class="bars">${rows.map(([n,v,c])=>`<dt>${n}</dt><dd><span class="bar" style="--w:${v}%;--bar:var(${c})"></span></dd><dd class="bar-value">${v}</dd>`).join('')}</dl><div class="bars" aria-hidden="true"><span></span><div class="bars-grid"><span>0</span><span>25</span><span>50</span><span>75</span><span>100</span></div><span></span></div></div>`;
}
function insights(){
  const cols=[
    ['Strengths','strength','--c5',[['Confirmed strength','Cross-team process redesign','Led a redesign across three departments and documented who owns each handoff. This is the clearest match to Operations Manager duties in the fictional profile.'],['Confirmed strength','Reporting others rely on','Introduced shared reporting used across teams. Reporting is a common requirement and is already backed by a confirmed example.']]],
    ['Path to success','path','--c2',[['Next move','Turn the redesign into a measured outcome','Add one number you can verify, such as time saved per handoff. A measured outcome strengthens both the resume and interview answers.'],['Next move','Show budget responsibility','Ask to own a small budget line. It closes the largest evidence gap without changing jobs.']]],
    ['Hurdles','hurdle','--c6',[['Evidence gap','People management is unconfirmed','The profile shows coordination but no direct reports. Some postings require formal people management; others accept leading cross-team work. Check each posting.'],['Evidence gap','Title reads as a lead, not a manager','“Operations lead” can be read as an individual contributor title. Describe scope and decisions owned so the level is clear.']]]
  ];
  return `<div class="insight-columns">${cols.map(([h,ic,tone,items])=>`<div class="insight-col" style="--tone:var(${tone})"><h3>${svgIcon(ic)}${h}</h3>${items.map(([chip,t,txt])=>`<article class="insight"><span class="chip">${chip}</span><h4>${t}</h4>${more(txt,3)}</article>`).join('')}</div>`).join('')}</div>`;
}
function renderAnalytics(){
 const old=state.runState==='stale'?' <span class="status warn">Based on an earlier profile version</span>':'';
 const tiles=`<div class="stat-tiles" role="list" aria-label="Occupation wage benchmark, fictional"><div class="stat-tile" role="listitem"><span class="stat-value">$94k</span><span class="stat-label">25th percentile · Operations managers, Denver</span></div><div class="stat-tile mid" role="listitem"><span class="stat-value">$116k</span><span class="stat-label">Median · same occupation and area</span></div><div class="stat-tile" role="listitem"><span class="stat-value">$141k</span><span class="stat-label">75th percentile · annual wages</span></div><div class="stat-tile unavailable" role="listitem"><span class="stat-value">Not available yet</span><span class="stat-label">Personalized comparable pay</span>${action('explain-range','Why unavailable?')}</div></div>`;
 return head('Career analytics','Your profile, the occupation benchmark and what to work on next. Every number on this page is a fictional example, not a prediction of your pay.',action('explain-range','Explain this range'))+
 `<div class="dash">${tiles}<p class="small">Occupation-wide benchmark for the whole occupation in one area, not a personal salary or offer. Illustrative BLS-style figures, not live data.${old}</p>`+
 `<section class="next-move" aria-labelledby="next-move-heading"><div><span class="status">Recommended next move</span><h2 id="next-move-heading">Write up the three-department process redesign</h2><p>Your strongest confirmed work, turned into evidence an employer can read. About two weeks of light effort. The charts below show why.</p></div>${link('roadmaps','Add to my roadmap','primary')}</section>`+
 `<div class="dash-row-3">${panel('Evidence strength','How well confirmed facts cover this role',evidenceGauge())}${panel('Profile completeness','What the agent can already use',completenessRing())}${panel('Skills diagram','Confirmed evidence against typical role requirements',skillsRadar())}</div>`+
 `<div class="dash-row-2">${panel('Compensation, with definitions','Annual wage interval for the occupation',intervalPlot([marketRows[0]])+`<p class="panel-foot">Advertised pay and annual wages are different measures. Desired pay is your preference, not evidence. A personalized range needs a qualified cohort of current employer-disclosed pay for comparable duties, level, location and work arrangement.</p>`)}${panel('What supports this direction','',`<ul class="evidence-list"><li><strong>Confirmed</strong><span>Cross-team operations work</span></li><li><strong>Confirmed</strong><span>Workflow redesign and reporting</span></li><li><strong>Needs evidence</strong><span>Budget ownership and people management</span></li><li><strong>Unknown</strong><span>Employer-disclosed comparable pay in the chosen market</span></li></ul>`)}</div>`+
 panel('Action priority matrix','Ordered by how much each action strengthens your evidence. Time and effort are planning estimates.',actionMatrix())+
 panel('Industries related to your experience','How closely operations roles in each industry match your confirmed duties, 0–100',industryBars())+
 insights()+
 panel('Role directions to compare','',`<div class="role-list"><div class="artifact-row"><div><h3>Operations Manager</h3><p>Closest match to the confirmed responsibilities.</p></div><span class="status">Current target</span></div><div class="artifact-row"><div><h3>Program Manager</h3><p>Potential stretch; clarify program ownership first.</p></div>${action('choose-role','Explore role')}</div></div><div class="note"><strong>What a real report must disclose</strong><p>Dataset and release date, wage definition, occupation code, geography, suppressed cells, comparable cohort, exclusions and limits.</p></div><div class="button-row section-actions">${link('heatmap','Compare markets','primary')}${link('roadmaps','Plan toward this role')}</div>`,' ')+
 `</div>`;
}
const MARKET_STEPS=['Where you live','Places to consider','Side by side','Pick your target'];
const findMarket=city=>marketRows.find(m=>m.city===city)||{city,pay:'Unavailable'};
const midpoint=m=>m.low==null?null:(m.low+m.high)/2;
function relocationLabel(city){
  if(city===state.city)return 'Home market';
  return state.canRelocate?'Relocation possible for on-site work':'Relocation required for on-site work';
}
function differenceFromHome(m){
  const home=midpoint(findMarket(state.city)),here=midpoint(m);
  if(m.city===state.city)return 'Your reference point';
  if(home==null||here==null)return 'No example value to compare';
  const diff=Math.round(here-home);
  return diff===0?'Same example midpoint as home':`${diff>0?'+':'−'}$${Math.abs(diff)}k example midpoint vs. home`;
}
function marketStepper(){
  return `<ol class="wizard-steps" aria-label="Market comparison steps">${MARKET_STEPS.map((label,i)=>{
    const n=i+1,current=n===state.marketStep,done=n<state.marketStep;
    const body=`<span class="wizard-num" aria-hidden="true">${done?'✓':n}</span><span>${label}</span>`;
    return `<li class="${current?'current':''} ${done?'done':''}" ${current?'aria-current="step"':''}>${done?`<button type="button" class="wizard-jump" data-action="market-step" data-step="${n}">${body}<span class="visually-hidden"> (completed, go back)</span></button>`:body}</li>`;
  }).join('')}</ol>`;
}
function stepHead(title,lead){
  return `<h2 id="market-step-heading" tabindex="-1">Step ${state.marketStep} of 4 · ${title}</h2><p class="section-lead">${lead}</p>`;
}
function marketStepBody(){
  const step=state.marketStep;
  if(step===1){
    const cities=marketRows.map(m=>m.city);if(!cities.includes(state.city))cities.unshift(state.city);
    return stepHead('Where you live now','Your home market is the reference point. It is only used to show which places would need a move for on-site work, and it is the same value as the goal on Career profile.')+
    `<div class="wizard-panel"><label class="field" for="home-market">Home market</label><select id="home-market">${cities.map(c=>`<option ${c===state.city?'selected':''}>${clean(c)}</option>`).join('')}</select><p class="small">Fictional person · saved in this browser session only.</p></div><div class="button-row wizard-nav">${action('market-next','Next: choose places','primary')}</div>`;
  }
  if(step===2){
    const options=marketRows.filter(m=>m.city!==state.city);
    const full=state.selectedMarkets.length>=3;
    return stepHead('Places to consider',`Choose up to three places to compare with your home, ${clean(state.city)}. Chosen places light up in the chart.`)+
    `<div class="wizard-columns"><div><label class="field" for="market-search">Search places</label><input id="market-search" type="search" placeholder="Try Seattle" value="${clean(state.marketQuery)}" autocomplete="off"><fieldset class="market-options"><legend class="field">Places</legend>${options.map((m,i)=>{const on=state.selectedMarkets.includes(m.city);return `<label class="market-option ${on?'checked':''}" data-market="${clean(m.city)}"><input type="checkbox" id="market-opt-${i}" data-market-choice="${clean(m.city)}" ${on?'checked':''} ${full&&!on?'disabled':''}><span><strong>${clean(m.city)}</strong><span class="small">${clean(m.pay)} · ${relocationLabel(m.city)}</span></span></label>`;}).join('')}</fieldset><p id="market-count" class="wizard-count" aria-live="polite">${state.selectedMarkets.length} of 3 chosen${full?' · remove one to choose another':''}</p></div><div>${intervalPlot(marketRows,false,true)}</div></div>`+
    `<div class="button-row wizard-nav">${action('market-back','Back')}<button type="button" class="button primary" data-action="market-next" ${state.selectedMarkets.length?'':'disabled'}>Next: compare side by side</button></div>`;
  }
  if(step===3){
    const rows=[state.city,...state.selectedMarkets].map(findMarket);
    return stepHead('Side by side',`Your home and the ${state.selectedMarkets.length===1?'place':'places'} you chose. Differences use fictional example midpoints, not a pay prediction.`)+
    `<div class="market-cards">${rows.map(m=>`<article class="market-card ${m.city===state.city?'home':''}"><span class="label">${m.city===state.city?'Home':'Option'}</span><h3>${clean(m.city)}</h3><p class="market-range">${clean(m.pay)}</p><p class="market-diff">${differenceFromHome(m)}</p><span class="status ${m.city===state.city?'':'muted'}">${relocationLabel(m.city)}</span></article>`).join('')}</div>${intervalPlot(rows,false,true)}`+
    `<div class="button-row wizard-nav">${action('market-back','Back')}${action('market-next','Next: pick a target','primary')}</div>`;
  }
  if(!state.marketSaved){
    const pick=state.selectedMarkets.includes(state.marketToSave)?state.marketToSave:state.selectedMarkets[0];
    return stepHead('Pick your target','Your target is a place to work toward. Your home stays the same.')+
    `<fieldset class="market-options"><legend class="field">Target market</legend>${state.selectedMarkets.map((c,i)=>`<label class="market-option ${c===pick?'checked':''}"><input type="radio" name="target-choice" id="target-opt-${i}" value="${clean(c)}" ${c===pick?'checked':''}><span><strong>${clean(c)}</strong><span class="small">${clean(findMarket(c).pay)} · ${differenceFromHome(findMarket(c))}</span></span></label>`).join('')}</fieldset>`+
    `<div class="button-row wizard-nav">${action('market-back','Back')}${action('save-market','Save as my target','primary')}</div>`;
  }
  return stepHead('Target saved','Here is exactly what changed.')+
  `<div class="wizard-confirm" id="market-confirmation"><dl class="change-list"><div><dt>Target market</dt><dd><span class="was">${clean(state.previousTarget||'Not selected')}</span> → <strong>${clean(state.targetMarket)}</strong></dd></div><div><dt>Home market</dt><dd><strong>${clean(state.city)}</strong> (unchanged)</dd></div></dl><h3>Where you will see it</h3><ul class="change-effects"><li>Your goal on Career agent and Career profile now shows Target: ${clean(state.targetMarket)}.</li><li>Your career brief is marked out of date. No new analysis ran.</li><li>Saved in this browser session only.</li></ul></div>`+
  `<div class="button-row wizard-nav">${link('roadmaps',`Plan toward ${clean(state.targetMarket)}`,'primary')}${link('agent','Back to Career agent')}${action('market-restart','Compare again')}</div>`;
}
function renderHeatmap(){
 return head('Find a market that fits your life','Four short steps. All wage values are fictional examples; remote-job eligibility cannot be checked here.')+
 `<section class="section market-wizard"><div class="note" id="market-goal-summary"><strong>Home market: ${clean(state.city)} · Saved target market: ${clean(state.targetMarket||'Not selected')}</strong></div>${marketStepper()}<div class="wizard-body">${marketStepBody()}</div></section>`+
 section('Remote eligibility',`<div class="note">“Remote” on a listing does not mean eligible from every U.S. location. This prototype has no live listings or state restrictions, so remote eligibility remains unknown. A relocation label refers only to on-site work relative to the home market.</div>`);
}
function applyMarketSearch(){
  const query=state.marketQuery.trim().toLowerCase();
  if(state.marketStep!==2)return;
  document.querySelectorAll('#view .market-option[data-market]').forEach(row=>row.hidden=!row.dataset.market.toLowerCase().includes(query));
  document.querySelectorAll('#view .interval-chart .interval-row').forEach(row=>row.hidden=!row.dataset.market.toLowerCase().includes(query));
}
/** Home is one value shared with the profile goal. A target equal to home is cleared. */
function setHome(city){
  if(!city||city===state.city)return false;
  state.city=city;
  state.selectedMarkets=state.selectedMarkets.filter(m=>m!==city);
  if(state.targetMarket===city)state.targetMarket='';
  state.marketSaved=false;state.runState='stale';
  return true;
}
function goToMarketStep(step){
  state.marketStep=Math.min(4,Math.max(1,step));
  syncThemeControls();
render();
  const heading=$('#market-step-heading');
  heading?.focus({preventScroll:true});
  heading?.closest('.market-wizard')?.scrollIntoView({block:'start',behavior:'instant'});
}
function renderRoadmaps(){
 const routes=[['closest','Closest fit','Build on existing operations leadership.','Clarify scope and prepare targeted examples.'],['stretch','Higher ambition','Explore program leadership after documenting cross-team ownership.','More evidence and interview preparation needed.'],['steady','Steadier transition','Keep the current role while testing adjacent opportunities.','Lower weekly effort; a longer timeline.']];
 const selected=routes.find(r=>r[0]===state.selectedRoute);
 const tasks=[['fact','Document one measurable process improvement','This week · 30 minutes'],['resume','Tailor the resume to the target role','Within 30 days · 2 hours'],['market','Compare two eligible markets','Within 60 days · 1 hour'],['interview','Prepare two interview stories','Within 90 days · 2 hours']];
 return head('Turn a direction into action','Choose a practical route. Change the effort assumptions and keep your own progress.',link('materials','Open materials'))+
 section('Choose your path',`<div class="grid-3">${routes.map(r=>`<div class="route-choice ${state.selectedRoute===r[0]?'active':''}"><span class="status ${state.selectedRoute===r[0]?'':'muted'}">${state.selectedRoute===r[0]?'Selected route':'Option'}</span><h3>${r[1]}</h3><p>${r[2]}</p><p class="small">${r[3]}</p><button type="button" class="button ${state.selectedRoute===r[0]?'selected':''}" data-action="select-route" data-route="${r[0]}" aria-pressed="${state.selectedRoute===r[0]}">${state.selectedRoute===r[0]?'Selected':'Choose route'}</button></div>`).join('')}</div>`)+
 section('Your plan',`<div class="scenario-banner"><div><h2>${selected[1]} · ${clean(state.role)}</h2><p>${clean(state.weeklyHours)} available each week. Dates are editable planning assumptions, never promised outcomes.</p></div>${action('adjust-effort','Adjust time')}</div><div class="paper milestone-rail"><p class="plan-progress">${state.completedTasks.length} of ${tasks.length} steps completed in this example</p>${tasks.map(([id,label,when])=>`<div class="task-row ${state.completedTasks.includes(id)?'complete':''}"><input type="checkbox" id="task-${id}" data-task="${id}" ${state.completedTasks.includes(id)?'checked':''}><label for="task-${id}">${label}<small>${when}</small></label></div>`).join('')}</div>`)+
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
  if(state.page==='heatmap')applyMarketSearch();
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
  if(a==='answer-no'||a==='answer-yes'){state.canRelocate=a==='answer-yes';state.runState='complete';render();notify('Relocation preference recorded. No listing eligibility was checked.');}
  if(a==='choose-role'){state.role='Program Manager';state.runState='stale';render();notify('Target changed; earlier analysis needs a refresh.');}
  if(a==='explain-range'){openAssistant();sendPrompt('Explain this pay range');}
  if(a==='market-next'){if(state.marketStep===2&&!state.selectedMarkets.length)return notify('Choose at least one place first.');goToMarketStep(state.marketStep+1);}
  if(a==='market-back'){state.marketSaved=false;goToMarketStep(state.marketStep-1);}
  if(a==='market-step'){state.marketSaved=false;goToMarketStep(Number(trigger.dataset.step));}
  if(a==='market-restart'){state.marketSaved=false;state.selectedMarkets=[];state.marketQuery='';goToMarketStep(1);}
  if(a==='save-market'){
    const target=document.querySelector('input[name="target-choice"]:checked')?.value;
    if(!target||!state.selectedMarkets.includes(target))return notify('Choose one of your places first.');
    state.previousTarget=state.targetMarket;state.targetMarket=target;state.marketToSave=target;state.marketSaved=true;
    if(state.previousTarget!==target)state.runState='stale';
    goToMarketStep(4);
    notify(`Saved target: ${target}. Home remains ${state.city}. Earlier brief needs refresh; no new analysis ran.`);
  }
  if(a==='select-route'){state.selectedRoute=trigger.dataset.route;render();notify('Roadmap route selected.');}
  if(a==='adjust-effort'){state.weeklyHours=state.weeklyHours==='4 hours'?'2 hours':state.weeklyHours==='2 hours'?'8 hours':'4 hours';render();notify('Weekly effort assumption updated.');}
  if(a==='material-tab'){state.materialTab=trigger.dataset.tab;render();const next=document.querySelector(`[data-tab="${state.materialTab}"]`);next?.focus();}
  if(a==='save-material'){state[state.materialTab==='resume'?'resume':'summary']=$('#material-editor').value;state.editedResume=true;save();notify('Example draft saved in this browser session.');}
  if(a==='download-material'){const content=$('#material-editor').value;const blob=new Blob([content],{type:'text/plain;charset=utf-8'});const url=URL.createObjectURL(blob);const download=document.createElement('a');download.href=url;download.download=state.materialTab==='resume'?'sample-resume.txt':'sample-summary.txt';download.click();setTimeout(()=>URL.revokeObjectURL(url),1000);notify('Plain-text sample downloaded.');}
  if(a==='photo-demo'){notify('Photo handoff previewed. The real workflow will show your authenticated allowance before generation.');}
  if(a==='photo-jump'){state.materialTab='photos';save();}
  if(a==='toggle-theme'){setTheme(currentTheme()==='dark'?'light':'dark');notify(`${currentTheme()==='dark'?'Dark':'Light'} theme on. Saved for this browser.`);}
  if(a==='toggle-more'){const box=trigger.closest('.more');const open=box.classList.toggle('expanded');trigger.setAttribute('aria-expanded',String(open));trigger.textContent=open?'Show less':'Show more';}
  if(a==='matrix-all'){const m=$('#action-matrix');const open=m.classList.toggle('expanded');trigger.setAttribute('aria-expanded',String(open));trigger.textContent=open?'Show fewer actions':'Show all 5 actions';}
  if(a==='show-help'){notify('Choose any page. Use the state selector on Career agent to preview incomplete and error states. All data is fictional.');}
  if(a==='prompt'){openAssistant();sendPrompt(trigger.dataset.prompt);}
});
document.addEventListener('change',event=>{
 if(event.target.id==='demo-state'){state.runState=event.target.value;render();}
 if(event.target.id==='home-market'){const city=event.target.value;if(setHome(city)){render();$('#home-market')?.focus();notify(`Home market is now ${city}. Your profile goal shows the same value.`);}}
 if(event.target.name==='target-choice'){state.marketToSave=event.target.value;document.querySelectorAll('input[name="target-choice"]').forEach(r=>r.closest('.market-option').classList.toggle('checked',r.checked));save();}
 if(event.target.dataset.marketChoice){
   const m=event.target.dataset.marketChoice,id=event.target.id;
   if(event.target.checked&&!state.selectedMarkets.includes(m)&&state.selectedMarkets.length<3)state.selectedMarkets.push(m);
   if(!event.target.checked)state.selectedMarkets=state.selectedMarkets.filter(x=>x!==m);
   render();document.getElementById(id)?.focus();
 }
 if(event.target.matches('[data-task]')){const id=event.target.dataset.task;if(event.target.checked&&!state.completedTasks.includes(id))state.completedTasks.push(id);if(!event.target.checked)state.completedTasks=state.completedTasks.filter(x=>x!==id);event.target.closest('.task-row')?.classList.toggle('complete',event.target.checked);const progress=$('.plan-progress');if(progress)progress.textContent=`${state.completedTasks.length} of 4 steps completed in this example`;save();notify('Roadmap progress updated.');}
});
document.addEventListener('submit',event=>{
 if(event.target.id!=='goal-form')return;event.preventDefault();
 state.role=$('#goal-role').value.trim()||state.role;if(setHome($('#goal-city').value.trim()))state.marketStep=1;state.arrangement=$('#goal-arrangement').value;state.weeklyHours=$('#goal-hours').value;state.runState='stale';render();notify('Goal saved; existing report needs a refresh.');
});
document.addEventListener('input',event=>{
 if(event.target.id==='market-search'){state.marketQuery=event.target.value;applyMarketSearch();save();}
});
function openAssistant(){if(desktop()&&document.documentElement.classList.contains('assistant-collapsed'))setAssistantCollapsed(false,false);state.assistantOpen=true;$('#assistant').classList.add('open');$('#mobile-agent').setAttribute('aria-expanded','true');$('#assistant-input').focus();save();}
function closeAssistant(){state.assistantOpen=false;$('#assistant').classList.remove('open');$('#mobile-agent').setAttribute('aria-expanded','false');$('#mobile-agent').focus();save();}
function sendPrompt(prompt){
 const q=(prompt||$('#assistant-input').value).trim();if(!q)return;
 const answer=q.toLowerCase().includes('evidence')?'Evidence strength shows how many common requirements for the target role are backed by facts you confirmed. It is not a grade of you or your worth. Here, budget ownership and people management have no confirmed examples, so it reads Supported rather than Strong.':q.toLowerCase().includes('range')||q.toLowerCase().includes('pay')?'The benchmark is a fictional occupation-wide wage example. A personalized range is unavailable until current, licensed and comparable employer pay observations are qualified. It is not a prediction of your offer.':q.toLowerCase().includes('remote')?'Remote eligibility depends on each posting’s actual state or country restrictions. No live listings are connected to this prototype.':q.toLowerCase().includes('photo')?'Photos are optional. The career resume omits a headshot by default, and any paid photo action stays under your control.':'For this example, review the source facts on this page and choose the next action in the workspace. This is a scripted prototype response, not AI research.';
 state.messageHistory.push({page:state.page,mine:true,text:q},{page:state.page,mine:false,text:answer});$('#assistant-input').value='';renderAssistant();$('#assistant-messages').scrollTop=$('#assistant-messages').scrollHeight;save();
}
$('#assistant-send').addEventListener('click',()=>sendPrompt());
$('#assistant-input').addEventListener('keydown',event=>{if(event.key==='Enter')sendPrompt();});
$('#mobile-agent').addEventListener('click',openAssistant);
const desktop=()=>!matchMedia('(max-width: 980px)').matches;
/** Desktop: the assistant is open by default and can be collapsed; the choice is remembered. */
function setAssistantCollapsed(collapsed,focus=true){
  document.documentElement.classList.toggle('assistant-collapsed',collapsed);
  try{localStorage.setItem('career-assistant',collapsed?'collapsed':'open');}catch{}
  syncAssistantControls();
  if(focus)(collapsed?$('#assistant-reopen'):$('#assistant-input')).focus();
}
function syncAssistantControls(){
  const collapsed=document.documentElement.classList.contains('assistant-collapsed');
  $('#assistant-reopen').hidden=!collapsed;
  $('#assistant-reopen').setAttribute('aria-expanded',String(!collapsed));
  $('#assistant-close').setAttribute('aria-label',desktop()?'Collapse assistant':'Close assistant');
}
$('#assistant-close').addEventListener('click',()=>desktop()?setAssistantCollapsed(true):closeAssistant());
$('#assistant-reopen').addEventListener('click',()=>setAssistantCollapsed(false));
window.addEventListener('resize',syncAssistantControls);
syncAssistantControls();
$('#mobile-menu').addEventListener('click',()=>{const nav=$('#mobile-nav');nav.hidden=!nav.hidden;$('#mobile-menu').setAttribute('aria-expanded',String(!nav.hidden));});
document.addEventListener('keydown',event=>{if(event.key==='Escape'){if(state.assistantOpen)closeAssistant();else if(!$('#mobile-nav').hidden){$('#mobile-nav').hidden=true;$('#mobile-menu').setAttribute('aria-expanded','false');$('#mobile-menu').focus();}}});
syncThemeControls();
render();

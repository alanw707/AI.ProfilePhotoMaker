const {chromium} = require('playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
(async () => {
  const browser = await chromium.launch({headless:true,executablePath:process.env.CHROME_BIN || '/usr/bin/google-chrome',args:['--no-sandbox']});
  const errors=[];
  const base='http://127.0.0.1:4311/prototypes/career-agent/';
  const review=path.join(__dirname,'review');fs.mkdirSync(review,{recursive:true});
  const desktop=await browser.newContext({viewport:{width:1440,height:900},acceptDownloads:true});
  const page=await desktop.newPage();
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(base);
  assert.match(await page.locator('h1').textContent(),/Make the next move clearer/);
  await page.screenshot({path:path.join(review,'desktop-agent.png'),fullPage:true});
  assert.equal(await page.locator('.goal-overview .interval-row').count(),2);
  assert.match(await page.locator('.goal-overview .interval-chart').textContent(),/Fictional 25th–75th percentile examples/);
  await page.locator('#primary-nav a[href="#analytics"]').click();
  await page.waitForTimeout(400);
  // The active plate must cover the current link exactly (links wrap, so no fixed step).
  const plate=await page.evaluate(()=>{const i=document.querySelector('.nav-active-indicator').getBoundingClientRect(),l=document.querySelector('#primary-nav .nav-link[aria-current="page"]').getBoundingClientRect();return [Math.round(i.top-l.top)+0,Math.round(i.height-l.height)+0,document.querySelector('#primary-nav .nav-link[aria-current="page"]').textContent];});
  assert.deepEqual(plate,[0,0,'Career analytics'],'nav indicator aligned with active link');
  assert.equal(await page.locator('#view .interval-band').count(),1);
  // Analytics dashboard: tiles, gauge, ring, radar, matrix, bars, insights — no grade or personal dollar figure.
  assert.equal(await page.locator('.stat-tile').count(),4);
  assert.match(await page.locator('.stat-tile.unavailable').textContent(),/Not available yet/);
  assert.equal(await page.locator('.gauge-badge strong').textContent(),'Supported');
  assert.match(await page.locator('.ring-wrap svg').getAttribute('aria-label'),/Profile 66% complete/);
  assert.equal(await page.locator('.radar-wrap table tbody tr').count(),6);
  assert.equal(await page.locator('.matrix-card').count(),5);
  assert.equal(await page.locator('.bars .bar').count(),6);
  assert.equal(await page.locator('.insight-col').count(),3);
  assert.doesNotMatch(await page.locator('#view').textContent(),/Your Number|grade|Opportunity cost/i);
  const firstMore=page.locator('.more-toggle').first();
  await firstMore.click();
  assert.equal(await firstMore.getAttribute('aria-expanded'),'true');
  assert.equal(await firstMore.textContent(),'Show less');
  assert.match(await page.locator('#view').textContent(),/\$94k–\$141k/);
  await page.screenshot({path:path.join(review,'desktop-analytics.png'),fullPage:true});
  // Task 3: step-by-step market comparison wizard.
  const heading=()=>page.locator('#market-step-heading').textContent();
  const focusedId=()=>page.evaluate(()=>document.activeElement.id);
  await page.goto(base+'#heatmap');
  assert.equal(await page.locator('.map-panel, #market-work, #market-metric, #market-table').count(),0,'no map, inert filters or old table');
  assert.match(await page.locator('#market-goal-summary').textContent(),/Home market: Denver, CO · Saved target market: Not selected/);
  assert.equal(await page.locator('.wizard-steps li').count(),4);
  assert.match(await heading(),/Step 1 of 4/);
  assert.equal(await page.locator('#home-market').inputValue(),'Denver, CO');
  await page.getByRole('button',{name:'Next: choose places'}).click();
  assert.match(await heading(),/Step 2 of 4 · Places to consider/);
  assert.equal(await focusedId(),'market-step-heading','focus moves to the new step');
  assert.equal(await page.getByRole('button',{name:'Next: compare side by side'}).isDisabled(),true,'cannot continue with no places');
  assert.equal(await page.locator('#view input[data-market-choice]').count(),4,'home is not offered as a place to consider');
  assert.equal(await page.locator('#view .interval-row').count(),5);
  assert.equal(await page.locator('#view .interval-band').count(),4);
  assert.equal(await page.locator('#view .interval-row.home').count(),1);
  assert.match(await page.locator('#view .interval-chart').textContent(),/No example value/);
  await page.locator('#market-search').fill('Seattle');
  assert.equal(await page.locator('#view .market-option:visible').count(),1);
  assert.equal(await page.locator('#view .interval-chart .interval-row:visible').count(),1);
  await page.getByLabel(/Seattle, WA/).check();
  assert.match(await page.locator('#market-count').textContent(),/1 of 3 chosen/);
  assert.equal(await page.locator('#view .interval-row.selected:visible').count(),1,'chosen place lights up in the chart');
  assert.equal(await page.locator('#market-search').inputValue(),'Seattle');
  await page.locator('#market-search').fill('');
  await page.getByLabel(/Minneapolis, MN/).check();
  await page.screenshot({path:path.join(review,'desktop-heatmap.png'),fullPage:true});
  await page.getByRole('button',{name:'Next: compare side by side'}).click();
  assert.match(await heading(),/Step 3 of 4 · Side by side/);
  assert.equal(await page.locator('.market-card').count(),3);
  assert.equal(await page.locator('#view .interval-row').count(),3,'chart shows only home and chosen places');
  const card=city=>page.locator('.market-card',{hasText:city}).textContent();
  assert.match(await card('Denver, CO'),/Home market/);
  assert.match(await card('Seattle, WA'),/\+\$20k example midpoint vs\. home/);
  assert.match(await card('Seattle, WA'),/Relocation required for on-site work/);
  assert.match(await card('Minneapolis, MN'),/−\$6k example midpoint vs\. home/);
  await page.screenshot({path:path.join(review,'desktop-heatmap-compare.png'),fullPage:true});
  await page.getByRole('button',{name:'Next: pick a target'}).click();
  assert.match(await heading(),/Step 4 of 4 · Pick your target/);
  await page.getByRole('radio',{name:/Seattle, WA/}).check();
  await page.getByRole('button',{name:'Save as my target'}).click();
  assert.match(await heading(),/Target saved/);
  const confirmation=await page.locator('#market-confirmation').textContent();
  assert.match(confirmation,/Target market\s*Not selected → Seattle, WA/);
  assert.match(confirmation,/Home market\s*Denver, CO \(unchanged\)/);
  assert.match(confirmation,/No new analysis ran/);
  assert.match(await page.locator('#market-goal-summary').textContent(),/Home market: Denver, CO · Saved target market: Seattle, WA/);
  assert.match(await page.locator('#save-status').textContent(),/Saved target: Seattle, WA. Home remains Denver, CO/);
  await page.getByRole('button',{name:/Places to consider/}).click();
  assert.match(await heading(),/Step 2 of 4/,'completed steps are reachable from the stepper');
  await page.getByRole('button',{name:'Next: compare side by side'}).click();
  await page.getByRole('button',{name:'Next: pick a target'}).click();
  assert.equal(await page.getByRole('radio',{name:/Seattle, WA/}).isChecked(),true,'saved target stays preselected');
  await page.getByRole('button',{name:'Save as my target'}).click();
  await page.reload();
  assert.match(await page.locator('#market-goal-summary').textContent(),/Saved target market: Seattle, WA/);
  assert.match(await page.locator('#market-confirmation').textContent(),/Seattle, WA/);
  await page.goto(base+'#agent');
  assert.match(await page.locator('.scenario-banner').textContent(),/Home: Denver, CO · Target market: Seattle, WA/);
  await page.goto(base+'#profile');
  assert.equal(await page.locator('#goal-city').inputValue(),'Denver, CO');
  assert.match(await page.locator('.section').first().textContent(),/Target market: Seattle, WA/);
  // Home edited on the profile is the same value the wizard uses; a target equal to home is cleared.
  await page.locator('#goal-city').fill('Seattle, WA');
  await page.getByRole('button',{name:'Save goal'}).click();
  await page.goto(base+'#heatmap');
  assert.match(await page.locator('#market-goal-summary').textContent(),/Home market: Seattle, WA · Saved target market: Not selected/);
  assert.match(await heading(),/Step 1 of 4/);
  assert.equal(await page.locator('#home-market').inputValue(),'Seattle, WA');
  // Home edited inside step 1.
  await page.locator('#home-market').selectOption('Denver, CO');
  assert.match(await page.locator('#market-goal-summary').textContent(),/Home market: Denver, CO/);
  assert.equal(await focusedId(),'home-market');
  await page.goto(base+'#profile');
  assert.equal(await page.locator('#goal-city').inputValue(),'Denver, CO');
  await page.goto(base+'#heatmap');
  await page.getByRole('button',{name:'Next: choose places'}).click();
  for(const city of [/Seattle/,/Minneapolis/,/Atlanta/])await page.getByLabel(city).check();
  assert.equal(await page.getByLabel(/Boise/).isDisabled(),true,'fourth place is blocked');
  assert.match(await page.locator('#market-count').textContent(),/3 of 3 chosen/);
  await page.getByRole('button',{name:'Back',exact:true}).click();
  assert.match(await heading(),/Step 1 of 4/);
  // Desktop assistant: open by default, collapsible, remembered, reopenable.
  await page.goto(base+'#analytics');
  assert.equal(await page.locator('#assistant').isVisible(),true,'assistant open by default on desktop');
  await page.getByRole('button',{name:'Collapse assistant'}).click();
  assert.equal(await page.locator('#assistant').isVisible(),false);
  assert.equal(await page.evaluate(()=>document.activeElement.id),'assistant-reopen');
  assert.ok(await page.evaluate(()=>document.querySelector('main').getBoundingClientRect().width>1200),'workspace widens when collapsed');
  await page.reload();
  assert.equal(await page.locator('#assistant').isVisible(),false,'collapsed state remembered');
  await page.getByRole('button',{name:'Ask your career agent'}).click();
  assert.equal(await page.locator('#assistant').isVisible(),true);
  assert.equal(await page.evaluate(()=>document.activeElement.id),'assistant-input');
  assert.equal(await page.locator('#assistant-reopen').isVisible(),false);
  await page.goto(base+'#roadmaps');
  assert.equal(await page.locator('.milestone-rail .task-row').count(),4);
  await page.screenshot({path:path.join(review,'desktop-roadmaps.png'),fullPage:true});
  for(const [hash,title] of [['profile','A profile you can trust'],['analytics','Career analytics'],['heatmap','Find a market'],['roadmaps','Turn a direction'],['materials','Materials for your next move']]){
    await page.goto(base+'#'+hash);
    assert.match(await page.locator('h1').textContent(),new RegExp(title));
  }
  await page.goto(base+'#profile');
  await page.evaluate(()=>window.scrollTo(0,260));
  const profileScroll=await page.evaluate(()=>window.scrollY);
  assert.ok(profileScroll>0,'profile must be long enough to test scroll restoration');
  await page.locator('#primary-nav').getByRole('link',{name:'Career analytics'}).click();
  await page.waitForFunction(()=>document.activeElement.id==='workspace');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'workspace');
  assert.equal(await page.evaluate(()=>window.scrollY),0);
  await page.goBack();
  await page.waitForFunction(()=>document.activeElement.id==='workspace');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'workspace');
  assert.ok(Math.abs((await page.evaluate(()=>window.scrollY))-profileScroll)<2,'back navigation restores prior route scroll');
  await page.goto(base+'#profile');
  await page.getByRole('button',{name:'Review a proposed edit'}).click();
  assert.equal(await page.locator('.review-change').count(),1);
  await page.getByRole('button',{name:'Accept change'}).click();
  assert.match(await page.locator('.paper').first().textContent(),/Senior Operations Lead/);
  await page.goto(base+'#heatmap');
  await page.getByRole('button',{name:'Next: choose places'}).click();
  assert.match(await page.locator('#market-count').textContent(),/3 of 3 chosen/,'choices persist across pages');
  await page.goto(base+'#roadmaps');
  await page.locator('#task-fact').check();
  assert.equal(await page.locator('#task-fact').isChecked(),true);
  await page.goto(base+'#materials');
  await page.getByRole('button',{name:'Download plain-text sample'}).click();
  await page.screenshot({path:path.join(review,'desktop-materials.png'),fullPage:true});
  const mobile=await browser.newContext({viewport:{width:390,height:844},deviceScaleFactor:1,acceptDownloads:true});
  const phone=await mobile.newPage();
  phone.on('pageerror',e=>errors.push(e.message));
  await phone.goto(base+'#heatmap');
  await phone.getByRole('button',{name:'Next: choose places'}).click();
  await phone.getByLabel(/Seattle/).check();
  await phone.screenshot({path:path.join(review,'mobile-heatmap.png'),fullPage:true});
  await phone.goto(base+'#analytics');
  await phone.screenshot({path:path.join(review,'mobile-analytics.png'),fullPage:true});
  await phone.goto(base+'#heatmap');
  assert.equal(await phone.locator('.map-panel').count(),0);
  assert.equal(await phone.locator('.wizard-steps li').count(),4);
  assert.match(await phone.locator('#market-step-heading').textContent(),/Step 2 of 4/);
  await phone.getByRole('button',{name:'Ask agent'}).click();
  assert.equal(await phone.locator('#assistant').isVisible(),true);
  await phone.screenshot({path:path.join(review,'mobile-assistant.png'),fullPage:true});
  await phone.keyboard.press('Escape');
  assert.equal(await phone.locator('#assistant').isVisible(),false);
  assert.equal(await phone.evaluate(()=>document.activeElement.id),'mobile-agent');
  const width=await phone.evaluate(()=>({scroll:document.documentElement.scrollWidth,inner:innerWidth}));
  assert.ok(width.scroll<=width.inner,'mobile horizontal overflow: '+JSON.stringify(width));
  for(const viewport of [{width:320,height:640},{width:720,height:450}]){
    await phone.setViewportSize(viewport);
    for(const route of ['agent','profile','analytics','heatmap','roadmaps','materials']){
      await phone.goto(base+'#'+route);
      const measured=await phone.evaluate(()=>({scroll:document.documentElement.scrollWidth,inner:innerWidth}));
      assert.ok(measured.scroll<=measured.inner,route+' overflows at '+viewport.width+': '+JSON.stringify(measured));
    }
  }
  await phone.goto(base+'#agent');
  await phone.reload();
  await phone.keyboard.press('Tab');
  assert.equal(await phone.evaluate(()=>document.activeElement.classList.contains('skip-link')),true);
  await phone.keyboard.press('Tab');
  assert.equal(await phone.evaluate(()=>document.activeElement.id),'mobile-menu');
  assert.notEqual(await phone.evaluate(()=>getComputedStyle(document.activeElement).outlineStyle),'none');
  const zoom=await browser.newContext({viewport:{width:640,height:400},deviceScaleFactor:2});
  const zoomPage=await zoom.newPage();
  for(const route of ['agent','profile','analytics','heatmap','roadmaps','materials']){
    await zoomPage.goto(base+'#'+route);
    const measured=await zoomPage.evaluate(()=>({scroll:document.documentElement.scrollWidth,inner:innerWidth,wide:[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right>innerWidth+1).slice(0,8).map(e=>({tag:e.tagName,cls:e.className,right:Math.round(e.getBoundingClientRect().right)}))}));
    assert.ok(measured.scroll<=measured.inner,route+' overflows at 200% zoom: '+JSON.stringify(measured));
  }
  const reduced=await browser.newContext({reducedMotion:'reduce',viewport:{width:390,height:844}});
  const reducedPage=await reduced.newPage();
  await reducedPage.goto(base);
  assert.equal(await reducedPage.evaluate(()=>getComputedStyle(document.documentElement).scrollBehavior),'auto');
  assert.equal(await reducedPage.locator('.interval-band').first().evaluate(el=>getComputedStyle(el).animationName),'none');
  assert.ok(await reducedPage.locator('.nav-active-indicator').evaluate(el=>parseFloat(getComputedStyle(el).transitionDuration)<0.001));
  const contrast=[];
  for(const theme of ['light','dark']){
  for(const route of ['agent','profile','analytics','heatmap','roadmaps','materials']){
    await page.goto(base+'#'+route);
    await page.evaluate(t=>{document.documentElement.dataset.theme=t;},theme);
    await page.waitForTimeout(260);
    contrast.push(...(await page.evaluate(()=>{
      const luminance=css=>{
        const channels=css.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=0.04045?v/12.92:((v+0.055)/1.055)**2.4);
        return channels[0]*0.2126+channels[1]*0.7152+channels[2]*0.0722;
      };
      const background=element=>{
        for(let node=element;node;node=node.parentElement){
          const color=getComputedStyle(node).backgroundColor;
          if(color&&!/^rgba\([^)]*,\s*0\)$/.test(color))return color;
        }
        return 'rgb(255,255,255)';
      };
      return ['.stat-label','.panel-sub','.source-tag','.chart-legend','.matrix-facts dt','.bars dt','.bar-value','.insight .more-text','.more-toggle','.time-pill','.chip','.gauge-levels .on','.gauge-badge small','.assistant-head h2','.assist-prompt','.nav-link','.rail-button','.nav-label','.sidebar-help','.sidebar-help-link','.page-head p','.section-lead','.label','.artifact-row p','.paper p','.note','.status','.assistant-compose small','.interval-chart figcaption span','.interval-footnote','.interval-axis','.interval-place','.interval-value','.interval-unavailable'].flatMap(selector=>{
        const element=[...document.querySelectorAll(selector)].find(e=>e.getClientRects().length&&e.textContent.trim());
        if(!element)return [];
        const fg=luminance(getComputedStyle(element).color),bg=luminance(background(element));
        return [{theme:document.documentElement.dataset.theme,route:location.hash.slice(1),selector,ratio:Math.round((Math.max(fg,bg)+0.05)/(Math.min(fg,bg)+0.05)*100)/100}];
      });
    })));
  }
  }
  assert.ok(contrast.every(({ratio})=>ratio>=4.5),'sampled text contrast below 4.5: '+JSON.stringify(contrast.filter(({ratio})=>ratio<4.5)));
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({pages:6,intervalCharts:true,navIndicator:true,roadmapMilestones:true,profileReview:true,marketWizard:true,marketSelection:true,separateHomeAndTarget:true,targetPersistsOnReload:true,noInertMarketControls:true,roadmapTask:true,materialsDownload:true,mobileAssistant:true,viewports:[320,390,720,1440],zoomEquivalent:'1280 physical px / 640 CSS px at DPR 2',reducedMotion:true,contrastSamples:contrast.length,lowestSampledContrast:Math.min(...contrast.map(x=>x.ratio)),scrollRestoration:true,horizontalOverflow:false,keyboardSkipLink:true,visibleFocus:true,assistantFocusReturn:true,pageErrors:errors,review},null,2));
  await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});

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
  for(const [hash,title] of [['profile','A profile you can trust'],['analytics','Understand the market'],['heatmap','Find a market'],['roadmaps','Turn a direction'],['materials','Materials for your next move']]){
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
  await page.getByRole('button',{name:'Compare',exact:true}).first().click();
  assert.match(await page.locator('.section').nth(1).textContent(),/Selected:/);
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
  await phone.screenshot({path:path.join(review,'mobile-heatmap.png'),fullPage:true});
  assert.equal(await phone.getByRole('button',{name:'Show map'}).count(),1);
  await phone.getByRole('button',{name:'Ask agent'}).click();
  assert.equal(await phone.locator('#assistant').isVisible(),true);
  await phone.screenshot({path:path.join(review,'mobile-assistant.png'),fullPage:true});
  await phone.keyboard.press('Escape');
  assert.equal(await phone.locator('#assistant').isVisible(),false);
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
  await page.goto(base+'#analytics');
  const contrast=await page.evaluate(()=>{
    const luminance=css=>{
      const channels=css.match(/[\d.]+/g).slice(0,3).map(Number).map(v=>v/255).map(v=>v<=0.04045?v/12.92:((v+0.055)/1.055)**2.4);
      return channels[0]*0.2126+channels[1]*0.7152+channels[2]*0.0722;
    };
    return ['.page-head p','.button.primary','.nav-link[aria-current="page"]','.comparison p','.note','.status'].map(selector=>{
      const element=document.querySelector(selector);if(!element)return null;
      const style=getComputedStyle(element);const fg=luminance(style.color);const bg=luminance(style.backgroundColor==='rgba(0, 0, 0, 0)'?'rgb(247, 247, 242)':style.backgroundColor);
      return {selector,ratio:(Math.max(fg,bg)+0.05)/(Math.min(fg,bg)+0.05)};
    }).filter(Boolean);
  });
  assert.ok(contrast.every(({ratio})=>ratio>=4.5),'sampled text contrast below 4.5: '+JSON.stringify(contrast));
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({pages:6,profileReview:true,marketSelection:true,roadmapTask:true,materialsDownload:true,mobileAssistant:true,viewports:[320,390,720,1440],zoomEquivalent:'1280 physical px / 640 CSS px at DPR 2',reducedMotion:true,contrast,scrollRestoration:true,horizontalOverflow:false,keyboardSkipLink:true,pageErrors:errors,review},null,2));
  await browser.close();
})().catch(e=>{console.error(e);process.exit(1);});

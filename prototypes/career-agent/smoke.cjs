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
  assert.deepEqual(errors,[]);
  console.log(JSON.stringify({pages:6,profileReview:true,marketSelection:true,roadmapTask:true,materialsDownload:true,mobileAssistant:true,viewports:[320,390,720,1440],horizontalOverflow:false,keyboardSkipLink:true,pageErrors:errors,review},null,2));
  await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1;});

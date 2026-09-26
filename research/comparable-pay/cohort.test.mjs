import test from 'node:test';
import assert from 'node:assert/strict';
import {evaluate,quantile,RULE_VERSION} from './cohort.mjs';
import {covered,edgeCases} from './fixtures.mjs';
const asOf=new Date('2026-09-23T12:00:00.000Z');
test('three families across two metros reproduce versioned intervals',()=>{
  for(const [role,geography,low,high] of [['software','Denver, CO',110300,195200],['operations','Denver, CO',84300,156200],['nursing','Seattle, WA',97300,162200]]){
    const result=evaluate(covered,{role,geography,level:'senior',employmentType:'full-time'},asOf);
    assert.equal(result.rule,RULE_VERSION);assert.equal(result.decision,'observed interval');
    assert.equal(result.included,12);assert.equal(result.employers,6);assert.deepEqual([result.interval.low,result.interval.high],[low,high]);
  }
});
test('linear percentile interpolation is explicit',()=>{assert.equal(quantile([10,20,30,40],.25),17.5);assert.equal(quantile([10,20,30,40],.75),32.5);});
test('duplicate, unqualified pay, unknown remote, stale and unknown level are excluded',()=>{
  const result=evaluate([...covered,...edgeCases],{role:'software',geography:'Denver, CO',level:'senior',employmentType:'full-time'},asOf);
  assert.equal(result.included,13);
  for(const reason of ['duplicate requisition','work-location eligibility unknown','not employer-disclosed pay','outside 90-day lookback','unknown pay basis or annual hours','different or unknown level']) assert.equal(result.exclusionReasons[reason],1);
});
test('sparse observations and one employer fall back',()=>{
  const few=evaluate(covered.slice(0,7),{role:'software',geography:'Denver, CO',level:'senior'},asOf);
  assert.equal(few.interval,null);
  const oneEmployer=covered.filter(x=>x.role==='software').map(x=>({...x,employer:'one employer'}));
  assert.equal(evaluate(oneEmployer,{role:'software',geography:'Denver, CO'},asOf).interval,null);
});
test('unknown eligibility and incompatible pay cannot be silently normalized',()=>{
  const bad={...covered[0],id:'x',canonicalId:'x',eligible:null,currency:'EUR',basis:'hourly',annualHours:null};
  const result=evaluate([bad],{role:'software',geography:'Denver, CO'},asOf);
  assert.equal(result.included,0);
  assert.ok(result.exclusionReasons['work-location eligibility unknown']);assert.ok(result.exclusionReasons['unknown or non-USD currency']);
});

// Synthetic fixtures only. Never present these values as observed pay.
const origin='2026-09-22T12:00:00.000Z';
const families=[['software','Denver, CO',107000,182000],['operations','Denver, CO',81000,143000],['nursing','Seattle, WA',94000,149000]];
export const covered=families.flatMap(([role,geography,base,ceiling])=>Array.from({length:12},(_,i)=>({
  id:role+'-'+i,canonicalId:role+'-'+i,employer:'Synthetic employer '+(i%6+1),
  role,geography,level:'senior',employmentType:'full-time',eligible:true,employerDisclosed:true,
  currency:'USD',basis:'annual',low:base+i*1200,high:ceiling+i*1600,updatedAt:origin
})));
export const edgeCases=[
  {...covered[0],id:'duplicate-01',canonicalId:covered[0].canonicalId},
  {...covered[1],id:'unknown-remote',canonicalId:'remote-unknown',eligible:null},
  {...covered[2],id:'modeled',canonicalId:'modeled',employerDisclosed:false},
  {...covered[3],id:'stale',canonicalId:'stale',updatedAt:'2025-01-01T00:00:00Z'},
  {...covered[4],id:'hourly',canonicalId:'hourly',basis:'hourly',annualHours:2080,low:48,high:70},
  {...covered[5],id:'basis-unknown',canonicalId:'basis-unknown',basis:'unknown'},
  {...covered[6],id:'different-level',canonicalId:'different-level',level:'entry'}
];

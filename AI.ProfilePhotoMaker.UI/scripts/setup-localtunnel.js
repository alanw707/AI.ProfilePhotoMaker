#!/usr/bin/env node

/**
 * Helper script to set up localtunnel configuration
 * Usage: node scripts/setup-localtunnel.js [backend-url]
 */

const fs = require('fs');
const path = require('path');

function updateEnvironmentFile(backendUrl) {
  const envPath = path.join(__dirname, '../src/environments/environment.localtunnel.ts');
  
  let content = fs.readFileSync(envPath, 'utf8');
  
  // Replace the external URLs with the provided backend URL
  content = content.replace(
    /externalApiUrl: '.*?'/,
    `externalApiUrl: '${backendUrl}/api'`
  );
  content = content.replace(
    /externalAppUrl: '.*?'/,
    `externalAppUrl: '${backendUrl}'`
  );
  
  fs.writeFileSync(envPath, content);
  console.log(`✅ Updated environment.localtunnel.ts with backend URL: ${backendUrl}`);
}

function main() {
  const backendUrl = process.argv[2];
  
  if (!backendUrl) {
    console.log('🔧 Localtunnel Setup Instructions:');
    console.log('');
    console.log('1. Start your backend server (usually on port 5035)');
    console.log('2. Create a localtunnel for your backend:');
    console.log('   npx localtunnel --port 5035');
    console.log('');
    console.log('3. Copy the backend tunnel URL and run:');
    console.log('   node scripts/setup-localtunnel.js <backend-tunnel-url>');
    console.log('');
    console.log('4. Start the frontend with localtunnel configuration:');
    console.log('   npm run start:localtunnel');
    console.log('');
    console.log('5. Create a localtunnel for your frontend:');
    console.log('   npx localtunnel --port 4200');
    console.log('');
    console.log('Example:');
    console.log('   node scripts/setup-localtunnel.js https://backend-abc123.loca.lt');
    return;
  }
  
  // Validate URL
  try {
    new URL(backendUrl);
  } catch (error) {
    console.error('❌ Invalid URL provided:', backendUrl);
    console.error('Please provide a valid URL like: https://backend-abc123.loca.lt');
    return;
  }
  
  updateEnvironmentFile(backendUrl);
  
  console.log('');
  console.log('🚀 Next steps:');
  console.log('1. Start the frontend: npm run start:localtunnel');
  console.log('2. Create frontend tunnel: npx localtunnel --port 4200');
  console.log('3. Access your app via the frontend tunnel URL');
  console.log('');
  console.log('💡 Pro tip: You can also set the backend URL dynamically by opening');
  console.log('   the browser console and running:');
  console.log(`   localStorage.setItem('BACKEND_URL', '${backendUrl}')`);
}

main();
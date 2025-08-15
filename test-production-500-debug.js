// Debug script to test various scenarios that might cause 500 errors
const testScenarios = [
    {
        name: "No Authentication",
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ imageUrl: "test.jpg", enhancementType: "professional" })
    },
    {
        name: "Invalid Token", 
        headers: { 
            'Content-Type': 'application/json',
            'Authorization': 'Bearer invalid-token-123'
        },
        body: JSON.stringify({ imageUrl: "test.jpg", enhancementType: "professional" })
    },
    {
        name: "Malformed JSON",
        headers: { 'Content-Type': 'application/json' },
        body: '{"imageUrl": "test.jpg", "enhancementType": "professional"'  // Missing closing brace
    },
    {
        name: "Missing Required Fields",
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ enhancementType: "professional" })  // Missing imageUrl
    },
    {
        name: "Invalid Content Type",
        headers: { 'Content-Type': 'text/plain' },
        body: JSON.stringify({ imageUrl: "test.jpg", enhancementType: "professional" })
    },
    {
        name: "Empty Body",
        headers: { 'Content-Type': 'application/json' },
        body: ""
    }
];

async function testScenario(scenario) {
    try {
        console.log(`\n--- Testing: ${scenario.name} ---`);
        
        const response = await fetch('https://api.aiprofilephotomaker.com/api/replicate/enhance', {
            method: 'POST',
            headers: scenario.headers,
            body: scenario.body
        });
        
        const responseText = await response.text();
        
        console.log(`Status: ${response.status} ${response.statusText}`);
        console.log(`Content-Type: ${response.headers.get('content-type')}`);
        console.log(`Response: ${responseText.substring(0, 200)}${responseText.length > 200 ? '...' : ''}`);
        
        if (response.status === 500) {
            console.log(`🚨 FOUND 500 ERROR IN: ${scenario.name}`);
            console.log(`Full Response: ${responseText}`);
        }
        
    } catch (error) {
        console.log(`Error in ${scenario.name}: ${error.message}`);
    }
}

async function runAllTests() {
    console.log('🔍 Testing production API for 500 errors...');
    console.log('Target: https://api.aiprofilephotomaker.com/api/replicate/enhance');
    
    for (const scenario of testScenarios) {
        await testScenario(scenario);
        await new Promise(resolve => setTimeout(resolve, 1000)); // 1 second delay
    }
    
    console.log('\n✅ All tests completed');
}

runAllTests().catch(console.error);
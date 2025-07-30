#!/usr/bin/env python3

"""
Azure Infrastructure Deployment using Azure SDK for Python
Alternative to Azure CLI that bypasses API issues
"""

import os
import sys
import json
import time
import argparse
import logging
from datetime import datetime
from typing import Dict, Optional

try:
    from azure.identity import DefaultAzureCredential, AzureCliCredential
    from azure.mgmt.resource import ResourceManagementClient
    from azure.mgmt.resource.resources.models import ResourceGroup, Deployment, DeploymentProperties, DeploymentMode
    from azure.core.exceptions import ResourceNotFoundError, HttpResponseError
except ImportError:
    print("ERROR: Azure SDK not installed. Please install with:")
    print("pip install azure-identity azure-mgmt-resource")
    sys.exit(1)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class AzureDeployer:
    """Azure Infrastructure Deployment using Python SDK"""
    
    def __init__(self, subscription_id: Optional[str] = None):
        """Initialize Azure Deployer
        
        Args:
            subscription_id: Azure subscription ID. If None, uses default from CLI
        """
        self.subscription_id = subscription_id
        self.credential = self._get_credential()
        self.resource_client = ResourceManagementClient(
            credential=self.credential,
            subscription_id=self.subscription_id
        )
        
        # If subscription_id not provided, get from credential
        if not self.subscription_id:
            try:
                # Try to get subscription from Azure CLI
                import subprocess
                result = subprocess.run(['az', 'account', 'show', '--query', 'id', '-o', 'tsv'], 
                                      capture_output=True, text=True)
                if result.returncode == 0:
                    self.subscription_id = result.stdout.strip()
                    logger.info(f"Using subscription from Azure CLI: {self.subscription_id}")
                else:
                    logger.error("Could not determine subscription ID")
                    raise ValueError("Subscription ID required")
            except Exception as e:
                logger.error(f"Failed to get subscription ID: {e}")
                raise
    
    def _get_credential(self) -> DefaultAzureCredential:
        """Get Azure credential using multiple authentication methods"""
        try:
            # Try multiple credential types in order
            credential = DefaultAzureCredential(
                exclude_interactive_browser_credential=False,
                exclude_shared_token_cache_credential=False,
                exclude_visual_studio_code_credential=True,
                exclude_managed_identity_credential=True,
                exclude_environment_credential=False
            )
            logger.info("Successfully initialized Azure credentials")
            return credential
        except Exception as e:
            logger.error(f"Failed to initialize Azure credentials: {e}")
            logger.info("Please run 'az login' or set up service principal authentication")
            raise

    def ensure_resource_group(self, resource_group_name: str, location: str) -> bool:
        """Ensure resource group exists, create if not
        
        Args:
            resource_group_name: Name of the resource group
            location: Azure location for the resource group
            
        Returns:
            True if resource group exists or was created successfully
        """
        try:
            # Check if resource group exists
            rg = self.resource_client.resource_groups.get(resource_group_name)
            logger.info(f"Resource group '{resource_group_name}' already exists")
            return True
        except ResourceNotFoundError:
            logger.info(f"Creating resource group '{resource_group_name}' in {location}")
            try:
                rg_params = ResourceGroup(location=location, tags={
                    'Environment': 'Infrastructure',
                    'CreatedBy': 'Python-SDK-Deployer',
                    'CreatedAt': datetime.now().isoformat()
                })
                self.resource_client.resource_groups.create_or_update(
                    resource_group_name, rg_params
                )
                logger.info(f"Resource group '{resource_group_name}' created successfully")
                return True
            except Exception as e:
                logger.error(f"Failed to create resource group: {e}")
                return False
        except Exception as e:
            logger.error(f"Error checking resource group: {e}")
            return False

    def validate_template(self, resource_group_name: str, template: Dict, 
                         parameters: Dict, deployment_name: str) -> bool:
        """Validate ARM template before deployment
        
        Args:
            resource_group_name: Target resource group
            template: ARM template JSON
            parameters: Template parameters
            deployment_name: Name for the deployment
            
        Returns:
            True if template is valid
        """
        logger.info("Validating ARM template...")
        
        try:
            deployment_properties = DeploymentProperties(
                template=template,
                parameters=parameters,
                mode=DeploymentMode.incremental
            )
            
            deployment = Deployment(properties=deployment_properties)
            
            # Validate the deployment
            validation_result = self.resource_client.deployments.validate(
                resource_group_name=resource_group_name,
                deployment_name=deployment_name,
                parameters=deployment
            )
            
            if hasattr(validation_result, 'error') and validation_result.error:
                logger.error(f"Template validation failed: {validation_result.error}")
                return False
            
            logger.info("✅ Template validation passed!")
            return True
            
        except HttpResponseError as e:
            logger.error(f"Template validation failed with HTTP error: {e}")
            if hasattr(e, 'response') and e.response:
                logger.error(f"Response: {e.response.text}")
            return False
        except Exception as e:
            logger.error(f"Template validation failed: {e}")
            return False

    def deploy_template(self, resource_group_name: str, template: Dict, 
                       parameters: Dict, deployment_name: str, 
                       validate_only: bool = False) -> bool:
        """Deploy ARM template to Azure
        
        Args:
            resource_group_name: Target resource group
            template: ARM template JSON
            parameters: Template parameters
            deployment_name: Name for the deployment
            validate_only: If True, only validate template
            
        Returns:
            True if deployment successful
        """
        try:
            # Validate first
            if not self.validate_template(resource_group_name, template, parameters, deployment_name):
                return False
            
            if validate_only:
                logger.info("Validation completed. Skipping deployment.")
                return True
            
            logger.info(f"Starting deployment '{deployment_name}'...")
            
            deployment_properties = DeploymentProperties(
                template=template,
                parameters=parameters,
                mode=DeploymentMode.incremental
            )
            
            deployment = Deployment(properties=deployment_properties)
            
            # Start deployment
            deployment_async_operation = self.resource_client.deployments.begin_create_or_update(
                resource_group_name=resource_group_name,
                deployment_name=deployment_name,
                parameters=deployment
            )
            
            logger.info("⏳ Deployment started. Monitoring progress...")
            
            # Monitor deployment progress
            deployment_result = deployment_async_operation.result()
            
            if deployment_result.properties.provisioning_state == "Succeeded":
                logger.info("🎉 Deployment completed successfully!")
                
                # Show outputs if available
                if deployment_result.properties.outputs:
                    logger.info("📊 Deployment Outputs:")
                    for key, value in deployment_result.properties.outputs.items():
                        logger.info(f"  {key}: {value.get('value', 'N/A')}")
                
                return True
            else:
                logger.error(f"❌ Deployment failed with status: {deployment_result.properties.provisioning_state}")
                if deployment_result.properties.error:
                    logger.error(f"Error details: {deployment_result.properties.error}")
                return False
                
        except HttpResponseError as e:
            logger.error(f"Deployment failed with HTTP error: {e}")
            if hasattr(e, 'response') and e.response:
                logger.error(f"Response: {e.response.text}")
            return False
        except Exception as e:
            logger.error(f"Deployment failed: {e}")
            return False

    def get_deployment_status(self, resource_group_name: str, deployment_name: str) -> Optional[str]:
        """Get status of a deployment
        
        Args:
            resource_group_name: Resource group name
            deployment_name: Deployment name
            
        Returns:
            Deployment status or None if not found
        """
        try:
            deployment = self.resource_client.deployments.get(
                resource_group_name=resource_group_name,
                deployment_name=deployment_name
            )
            return deployment.properties.provisioning_state
        except ResourceNotFoundError:
            return None
        except Exception as e:
            logger.error(f"Error getting deployment status: {e}")
            return None

def load_json_file(file_path: str) -> Dict:
    """Load and parse JSON file
    
    Args:
        file_path: Path to JSON file
        
    Returns:
        Parsed JSON data
    """
    try:
        with open(file_path, 'r') as f:
            return json.load(f)
    except FileNotFoundError:
        logger.error(f"File not found: {file_path}")
        raise
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON in {file_path}: {e}")
        raise

def extract_parameters(parameters_json: Dict) -> Dict:
    """Extract parameters from Azure parameters file format
    
    Args:
        parameters_json: Parameters JSON from file
        
    Returns:
        Extracted parameters for deployment
    """
    if 'parameters' in parameters_json:
        # Extract 'value' from each parameter
        params = {}
        for key, param_obj in parameters_json['parameters'].items():
            if isinstance(param_obj, dict) and 'value' in param_obj:
                params[key] = {'value': param_obj['value']}
            else:
                params[key] = {'value': param_obj}
        return params
    else:
        return parameters_json

def main():
    """Main function"""
    parser = argparse.ArgumentParser(
        description='Deploy Azure infrastructure using Python SDK (bypasses Azure CLI API issues)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s                                    # Deploy to staging
  %(prog)s -e prod                           # Deploy to production  
  %(prog)s --validate                        # Validate template only
  %(prog)s -g my-rg -l westus2              # Custom resource group and location
  %(prog)s --template custom.json           # Use custom template file

Prerequisites:
  - Python 3.7+
  - pip install azure-identity azure-mgmt-resource
  - Azure authentication (az login or service principal)
        """
    )
    
    parser.add_argument('-e', '--environment', 
                       choices=['staging', 'prod', 'dev'],
                       default='staging',
                       help='Environment to deploy to (default: staging)')
    
    parser.add_argument('-g', '--resource-group',
                       help='Resource group name (default: ai-profile-photo-maker-{environment})')
    
    parser.add_argument('-l', '--location',
                       default='East US',
                       help='Azure location (default: East US)')
    
    parser.add_argument('--template',
                       default='main.json',
                       help='ARM template file (default: main.json)')
    
    parser.add_argument('--parameters',
                       help='Parameters file (default: parameters.{environment}.json)')
    
    parser.add_argument('--validate', action='store_true',
                       help='Validate template only, do not deploy')
    
    parser.add_argument('--subscription-id',
                       help='Azure subscription ID (uses default if not specified)')
    
    parser.add_argument('-v', '--verbose', action='store_true',
                       help='Enable verbose logging')
    
    args = parser.parse_args()
    
    # Configure logging level
    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)
    
    # Determine resource group name
    resource_group_name = args.resource_group or f"ai-profile-photo-maker-{args.environment}"
    
    # Determine parameters file
    parameters_file = args.parameters or f"parameters.{args.environment}.json"
    
    # Generate deployment name
    deployment_name = f"python-sdk-deployment-{datetime.now().strftime('%Y%m%d-%H%M%S')}"
    
    logger.info("🚀 Starting Azure deployment with Python SDK")
    logger.info(f"Environment: {args.environment}")
    logger.info(f"Resource Group: {resource_group_name}")
    logger.info(f"Location: {args.location}")
    logger.info(f"Template: {args.template}")
    logger.info(f"Parameters: {parameters_file}")
    logger.info(f"Deployment Name: {deployment_name}")
    
    try:
        # Check if files exist
        if not os.path.exists(args.template):
            logger.error(f"Template file not found: {args.template}")
            logger.info("Hint: If you have main.bicep, compile it first with: bicep build main.bicep")
            sys.exit(1)
        
        if not os.path.exists(parameters_file):
            logger.error(f"Parameters file not found: {parameters_file}")
            sys.exit(1)
        
        # Load template and parameters
        logger.info("📋 Loading template and parameters...")
        template = load_json_file(args.template)
        parameters_raw = load_json_file(parameters_file)
        parameters = extract_parameters(parameters_raw)
        
        # Initialize deployer
        logger.info("🔐 Initializing Azure connection...")
        deployer = AzureDeployer(subscription_id=args.subscription_id)
        
        # Ensure resource group exists
        if not deployer.ensure_resource_group(resource_group_name, args.location):
            logger.error("Failed to ensure resource group exists")
            sys.exit(1)
        
        # Deploy template
        success = deployer.deploy_template(
            resource_group_name=resource_group_name,
            template=template,
            parameters=parameters,
            deployment_name=deployment_name,
            validate_only=args.validate
        )
        
        if success:
            if args.validate:
                logger.info("✅ Template validation completed successfully!")
            else:
                logger.info("✅ Deployment completed successfully!")
                logger.info(f"🌐 Check deployment in Azure Portal:")
                logger.info(f"https://portal.azure.com/#@/resource/subscriptions/{deployer.subscription_id}/resourcegroups/{resource_group_name}/deployments")
        else:
            logger.error("❌ Deployment failed!")
            sys.exit(1)
            
    except KeyboardInterrupt:
        logger.warning("⚠️ Deployment interrupted by user")
        sys.exit(1)
    except Exception as e:
        logger.error(f"❌ Deployment failed with error: {e}")
        logger.info("💡 Alternative deployment methods:")
        logger.info("1. Try Azure Portal: python3 deploy_azure_sdk.py --help")
        logger.info("2. Try PowerShell: ./deploy-arm-direct.sh -m pwsh")
        logger.info("3. Try REST API: ./deploy-arm-direct.sh -m rest")
        sys.exit(1)

if __name__ == "__main__":
    main()
AzureDistributedService
=======================

Provides an framework for making RPC calls from frontends (WebRoles/WebSites) to backend VMs (WorkerRoles) using Azure Storage Queues. The system is easy to setup and the calls look like simple RPC calls. Because the frontend communicates with the backend through a storage queue the worker roles can be autoscaled trivially using Azure's built in auto-scaling.

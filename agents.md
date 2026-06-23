# BugerIAM - Microservice Fast Food Ordering System

* I want a microserves application written in c# using a lightweight database like sqlite if needed. 
* The services should communicate via gprc where appropriate and be loosely coupled, each service having its own discreet database if required.
* MessageBus technology should also be used where there are event driven communications required
* Any data sharing should be done by published interfaces. 
* I want to be able to replace or update services independently of all the others. 
* This will be a fast food ordering system that has a web frontend. 
* The services must support the entire lifecycle of the ordering process, through payment, preparing of the order, cooking, status updates, delivery, receipt generation and customer feedback. 
* Each service must have a supporting docker file and the entire project must have the relevant kubernetes manifests to run the whole application on a kubernetes cluster. 
* Manifests must include everything from deployments, services and ingress using the gateway API. 
* This must be a complete working solution with everything required to deploy straight into a kubernetes cluster

# Debug Logging
* When something doesn't work as expected and is fixed, document the issue and resolution in DEBUG.md for future reference

# Development Requirements
* Ensure that an appropriate number of tests are written and executed successfully after each code change
* Do not make any assumptions. For any questions you have always ask me
* Do not make anything up
* Always ensure that the code compiles after each code change and testing cycle
* Please use the git cli commands to add changes into the repo and commit with appropriate messaging
* Dont worry about branching for the time being, always work on the master branch

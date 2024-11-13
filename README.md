# eryph
Turn your Windows desktops and servers into a local cloud.  
Experience the evolution of virtual machines: With eryph, you don't just manage VMs - you breed catlets!

## Description

Here we build the core of eryph, which is the management infrastructure for building catlets on Hyper-V hosts.  
Other parts of the project - like clients - have their own repository: https://github.com/eryph-org/

**Documentation**  
User documentation can be found on https://www.eryph.io  

**Community Support**  
Please don't post questions as issues, use [Discussions](https://github.com/orgs/eryph-org/discussions) to ask questions.  
Issues and PRs are welcome, but please note that these are for development issues, not user support. 

## Build

We use Visual Studio 2022 for building.   
If you want to build and run eryph from code, please note that this repository don't contains signed versions of our driver package, which is only delivered with the released packages. 
Therefore you have to enable the use of unsigned drivers on your dev machine. 

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/eryph-org/eryph-org/tags). 

## License

This project is licensed under the Business Source License - see the [LICENSE](LICENSE) file for details.  
The licence allows you to use eryph for any purpose, except for use in a cluster and as the basis of a commercial hosting service.  
These usages require an additional commercial license. 

## Trademark notice

dbosoft and eryph are registered trademark of dbosoft GmbH in all EU member states.  
Hyper-V is a registered trademark of Microsoft® in several countries.  

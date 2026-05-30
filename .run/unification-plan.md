# Architecture unification plan (workflow wf_b7d568ab-112, 2026-05-30)

38 findings → 19 bypass-only, 9 keep, 10 need human judgment. Goal: remove eryph-zero
shortcuts that exist ONLY to bypass the new component-registration + config-distribution
architecture, so eryph-zero and the split runtime are one architecture (two packagings).

## Ordered steps

1. **[DONE]** Dead one-way wiring + stale comment. `ConfigureAsOneWayClient` is an *external*
   `Dbosoft.Rebus.IRebusTransportConfigurer` member → impl must stay (unused). Fixed the
   `Eryph.ApiEndpoint/HostComputeApiModuleExtensions` comment to describe the bidirectional endpoint.

2. **Relocate eryph-zero misplaced registrations + drop dead Container params.** Move
   `ITokenCertificateManager`→`TokenCertificateManager` and `IDbContextConfigurer<IdentityDbContext>`→
   `InMemoryIdentityDbContextConfigurer` from `Eryph-zero/HostIdentityModuleExtensions.AddIdentityModule(container)`
   into `ZeroContainerExtensions.Bootstrap()`. Remove the `Container` param from `AddIdentityModule` and
   the unused one from `AddControllerModule`; update Program.cs:428,430. (low risk)

3. **Move Identity registration + bus into shared `IdentityModule`.** In `IdentityModule.AddSimpleInjector`:
   `AddComponentRegistration(ComponentType.Identity, $"{QueueNames.IdentityServices}.{Environment.MachineName}", {identity: endpointResolver.GetEndpoint("identity")})` (no realizers). In `ConfigureContainer`:
   `ConfigureRebus(...)` on that queue using host `IRebusTransportConfigurer` + `Collection.Register(IHandleMessages<>, IdentityModule.Assembly)`. Collapse standalone `Eryph.Identity/HostIdentityModuleExtensions` to only register the RabbitMQ transport. Add `Eryph.Rebus` ref to the Identity module csproj. (medium)

4. **Supply in-mem transport to eryph-zero Identity.** Add a `IConfigureContainerFilter<IdentityModule>` in
   eryph-zero `HostIdentityModuleExtensions` calling `UseInMemoryBus(context.ModulesHostServices)`. Then
   eryph-zero Identity registers + advertises like ComputeApi. (medium)

## DECISIONS (2026-05-30) + status

- Steps 1-4: **DONE** (commits `20948b55` API, `419d8033` identity). eryph-zero green.
- Step 5: **min cut chosen.** Safe half DONE — eryph-zero feeds its determined endpoints into the
  controller `endpoints` config section so the Endpoints domain is authoritative there too. Fully
  removing the shared cross-wired `EndpointResolver` (Program.cs:395,431) is DEFERRED to the "full"
  distributed-consumption work (startup-time ASP.NET/SSL/JWT consumers read endpoints before the bus).
- Steps 6 & 7: **NOT refactored — ACCEPTED WORKAROUND, documented as NOT valid for real production
  split use** (user decision). The network-sync feature legitimately differs per packaging (eryph-zero
  = admin-less / command-based auto network build; split = a different flow), so a single bus contract
  is premature. In-code note added at `NetworkSyncServiceBridgeService`. The AgentControlService
  cross-wire (NetworkModule.cs:18) is the same category. Revisit when the split network-sync flow is designed.

## HOLD for user decision (new contracts / product calls — open questions)

5. **Endpoint resolution onto the Endpoints domain.** Register `DistributedEndpointResolver` AS
   `IEndpointResolver`; add `EndpointsConfigRealizer` to ComputeApi/Identity; compute API gets identity JWT
   authority from the distributed map; eryph-zero stops cross-wiring the host-built `EndpointResolver`
   (Program.cs:395,431) and feeds overrides via the controller `endpoints` config section. Keep a minimal
   local self-URL resolver per web module (irreducible bootstrap). Startup-ordering risk
   (SslEndpointService/SystemClientGenerator/JWT read base/identity before first ConfigSnapshot).
   Min viable cut = remove the shared cross-wired map (5d); gate 5b.

6. **Replace in-process network-sync bridge with a controller bus command.** New
   `SyncNetworksCommand`/`ValidateNetworkChangesCommand` → controller `NetworkSyncService`; agent named-pipe
   REBUILD_NETWORKS/VALIDATE_CHANGES dispatch over the bus; delete `NetworkSyncServiceBridgeService` +
   host-provided `INetworkSyncService` cross-wire (VmHostAgentModule.cs:114). NEW command contract +
   CLI operator-API behavior decision.

7. **Move controller→agent OVN chassis-stop onto the bus.** New `AgentControlCommand`/event from
   `SyncedOVNDatabaseNode.Stop` to the agent queue; handler invokes `OVNChassisService`. Keep
   `IAgentControlService` AGENT-LOCAL (named-pipe STOP/START + local chassis). Remove eryph-zero
   cross-wire of the shared `AgentControlService` into NetworkModule (NetworkModule.cs:18).

## KEEP (irreducible packaging differences, confirmed)
in-mem vs RabbitMQ transport; SQLite vs MariaDB store; Windows vs cross-platform OVN env; Windows CNG
cert storage; lock dir; **controller does not self-register (it is the registrar/authority)**; entitlements
(Identity/ComputeApi are endpoint producers, not domain consumers); agent-local `AgentControlService`;
VmHostAgent/ComputeApi module-level registration (the reference-correct shape).

## OPEN QUESTIONS
- GenePool: full registered component vs co-located capability (ComponentType.GenePoolAgent placeholder)? Recommend NOT bare registration.
- NetworkModule/OVN control plane (ComponentType.Network): self-register/advertise northbound, or controller-internal?
- Step 5 scope + startup ordering (how far now).
- Step 6 operator-API contract (sync request/reply vs operation; named-pipe vs controller admin entry).

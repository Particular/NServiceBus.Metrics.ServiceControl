# Engineering context

## About this repository

This repository forwards NServiceBus endpoint metrics to ServiceControl Monitoring.

- `src/NServiceBus.Metrics.ServiceControl/` — plugin implementation
- `src/NServiceBus.Metrics.ServiceControl.Tests/` and `src/NServiceBus.Metrics.ServiceControl.AcceptanceTests/` — unit and acceptance tests
- `.github/workflows/` — CI pipelines

## Start here

- [Component documentation](https://docs.particular.net/monitoring/metrics/install-plugin) — configure endpoint metrics reporting and identify monitored instances
- [Repository README](../README.md) — package overview and support links
- [Contributing](https://docs.particular.net/platform/contributing) — contribution process

## Decisions and rationale

- [Architecture and design decisions](decisions/)

### Decisions recorded in pull requests

- Keep the binary metrics writers with the ServiceControl-specific plugin because ServiceControl is their only consumer — [#573](https://github.com/Particular/NServiceBus.Metrics.ServiceControl/pull/573)

Keep this index current when a canonical source is added, replaced, or retired; link, do not copy.

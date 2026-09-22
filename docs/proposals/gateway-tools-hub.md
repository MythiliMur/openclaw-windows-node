# Gateway Tools Hub design

## Status

Draft companion design for the package-side tool bridge proposed in [openclaw/openclaw-windows-packaging#92](https://github.com/openclaw/openclaw-windows-packaging/pull/92).

## Purpose

The Windows Hub is the interactive desktop experience for registering an existing tool for the MXC-isolated Gateway. It is a local request and status client for the packaging broker. It must not become a second owner of Gateway policy, PATH, shims, cross-identity ACLs, or Gateway lifecycle.

## User flow

```text
Gateway Tools -> Add existing tool

Where is the tool installed?

- Desktop or machine installation: Browse for executable
- OpenClaw Gateway Tools: Open folder or Scan

Selected -> confirm registration -> broker validates -> Gateway-ready
```

The desktop or machine flow supports an executable the user installed through the provider's official distribution. The Gateway Tools flow lets the user extract a provider's official ZIP into a stable package-managed location without revealing the raw MXC account profile path.

Discovery may suggest candidates from public safe sources such as App Paths, installed-app registration, machine PATH, and public install locations. Discovery never auto-registers a tool and desktop PATH success is never presented as proof that the isolated Gateway can run the tool.

## Hub surfaces

### Tool list

The Hub lists only broker-provided, bounded state:

```text
Command      Executable source       Gateway executable    Authentication
--------     ------------------      ------------------    --------------
gog          Desktop installation    Ready                 Required
example      Gateway Tools           Verification failed   Unknown
```

It supports refresh, re-verify, enable/disable, replace executable, unregister, and opening the package-provided Gateway Tools folder. It does not display raw protected paths, MXC account names, credential locations, token state, browser data, or arbitrary command output.

### Register tool

The registration form collects:

- command alias;
- optional display name;
- selected executable or Gateway Tools scan result; and
- an explicit acknowledgement that the selected program will become executable by the isolated Gateway identity after broker verification.

The Hub submits a narrow local request such as:

```json
{
  "operation": "registerTool",
  "command": "gog",
  "displayName": "Google Workspace CLI",
  "source": "desktop",
  "selectedExecutable": "local-only"
}
```

The packaging broker receives the real local path through its authenticated local transport. It canonicalizes and persists it; it returns only a registration id and bounded verification state. The Hub never sends this value to Gateway chat/model context.

### Interactive setup

A tool can be executable-ready while authentication is not configured. For a broker-created runtime profile, the Hub offers **Open interactive setup**. It asks the packaging broker to launch the tool in the human desktop session with the registration's approved runtime profile.

The UI explains that browser OAuth may complete in the desktop session while supported configuration/token state is written to the Gateway runtime profile. It warns users not to paste credentials, callback URLs, codes, or tokens into chat.

The Hub provides provider-neutral actions:

```text
Open interactive setup
Open official setup documentation
Verify from Gateway
```

It does not encode Gog, Work IQ, Google, Microsoft, OAuth commands, or any provider-specific credential behavior.

## Status model

The Hub renders the broker state without inference:

```text
Discovered
Selected
Registered
Gateway verification pending
Gateway-ready
Runtime profile ready
Authentication required
Authentication pending
Authentication verified
Verification failed
```

The UI must preserve the distinction between executable availability and authentication. An agent-session installation makes the executable path simpler but does not itself authorize a provider account.

## Security and authority

- User approval is required before registering an executable.
- Hub communicates with the authenticated local package broker only.
- Hub does not write Gateway PATH, create shims, adjust ACLs, restart Gateway, or read the agent profile directly.
- The Hub does not read or transfer Credential Manager entries, browser cookies/profiles, token files, client-secret JSON, callback values, or account identities.
- Hub logs and diagnostics contain only bounded statuses and redacted failures.

## Temporary boundary

This UI is potentially temporary. It fills the Windows/MXC gap until OpenClaw has a first-class, upstream model for agent-session context and supported interactive login that tools, skills, and plugins can declare and consume directly.
---
name: comment-reviewer
description: Reviews C# code comments for XML documentation completeness, clarity, consistency, and aviation/Mode-S domain-specific standards. Use when reviewing code quality, checking documentation, or ensuring comments explain intent rather than mechanics.
allowed-tools: Read, Grep, Glob
---

# Code Comment Review Skill

Reviews code comments in the aeromux project to ensure high-quality, consistent, and accurate documentation.

## What This Skill Reviews

### 1. XML Documentation Completeness

**Public APIs** (classes, methods, properties) must have:
- `<summary>` - One-line description of what it does
- `<param>` - For each parameter, what it represents
- `<returns>` - What the method returns (if non-void)
- `<exception>` - What exceptions can be thrown and when
- `<remarks>` - Additional details, usage notes, or important context

**Example:**
```csharp
/// <summary>
/// Decodes Mode-S Extended Squitter airborne position messages.
/// </summary>
/// <param name="message">The 112-bit Extended Squitter message.</param>
/// <returns>Decoded position data, or null if CPR decoding fails.</returns>
/// <exception cref="ArgumentException">Thrown when message format is invalid.</exception>
/// <remarks>
/// Requires both even and odd CPR frames for global position decoding.
/// See ICAO Annex 10, Volume IV for CPR algorithm details.
/// </remarks>
public Position? DecodeAirbornePosition(byte[] message)
```

### 2. Comment Quality and Clarity

**Good comments explain WHY, not WHAT:**

❌ Bad (describes what code does):
```csharp
// Loop through all aircraft
foreach (var aircraft in trackedAircraft)
```

✅ Good (explains intent/reasoning):
```csharp
// Remove stale aircraft that haven't sent updates in 60 seconds
// to prevent memory buildup from aircraft that have landed
foreach (var aircraft in trackedAircraft)
```

**Check for:**
- Comments that add value beyond what code already says
- Clear explanations of complex algorithms or non-obvious logic
- Justification for design decisions
- Warning about edge cases or assumptions
- No redundant or outdated comments

### 3. Aviation/Mode-S Specific Standards

When documenting Mode-S and ADS-B message parsing:

**Always include:**
- **DF (Downlink Format)** - The message type (DF 0, 4, 5, 11, 17, 18, etc.)
- **TC (Type Code)** - For Extended Squitter (TC 1-31)
- **BDS (Binary Data Selector)** - For Comm-B messages (BDS 0,5, 1,0, 2,0, etc.)
- **ICAO reference** - When implementing standard algorithms

**Example:**
```csharp
/// <summary>
/// Parses Extended Squitter Airborne Velocity message (DF 17, TC 19).
/// </summary>
/// <remarks>
/// Decodes ground speed, heading, and vertical rate per ICAO Annex 10 Vol IV.
/// Supports both ground speed (subtype 1-2) and airspeed (subtype 3-4) encoding.
/// </remarks>
```

**Common terms to document:**
- CPR (Compact Position Reporting) and local vs global decoding requirements
- NIC (Navigation Integrity Category)
- NACp (Navigation Accuracy Category - Position)
- SIL (Surveillance Integrity Level)
- Parity checks and error detection
- Frame buffering and timeout requirements

### 4. Consistency Across Codebase

**Ensure:**
- Similar classes/methods use similar documentation patterns
- Terminology is consistent (e.g., don't mix "aircraft" and "plane")
- XML doc style matches across all parsers
- Comment formatting is uniform (spacing, capitalization, punctuation)

**Examples of consistency checks:**
- All message parser methods document DF/TC codes
- All tracker handlers explain update logic and assumptions
- All public properties have summaries
- All throws have `<exception>` tags

## Default Scope

When no specific files are mentioned, the review scope is limited to **changed or newly added files** relative to the `main` branch. Use `git diff --name-only main` and `git status` to determine which files to review.

When the user specifies files or directories explicitly, review those instead.

## How to Use This Skill

Simply ask me to review comments in any way:

**Examples:**
- "Review comments" (reviews changed/new files vs main)
- "Review comments in MessageParser.cs"
- "Check XML documentation in the tracking handlers"
- "Are these comments clear and following our standards?"
- "Review all comments in the Tracking namespace"

I will:
1. Determine review scope (changed files by default, or user-specified files)
2. Read the files in scope
3. Analyze comments against the standards above
4. Identify issues with specific line references and severity
5. Suggest improvements with examples

## Review Output Format

When I review, I'll categorize each finding by severity:

### Severity Levels

- **Critical** - Missing XML documentation on public API, factually incorrect comments, misleading documentation
- **Warning** - Comments that describe WHAT instead of WHY, missing aviation-specific context (DF/TC/BDS codes), undocumented magic numbers from specs
- **Info** - Minor wording improvements, consistency suggestions, optional enhancements

### Report Structure

**Issues Found** (grouped by severity):
- Severity level
- File path and line number
- Current comment (or lack thereof)
- Why it's problematic
- Suggested improvement

**Summary:**
- Counts per severity level
- Overall comment quality assessment

## Related Files

See [aviation-doc-standards.md](aviation-doc-standards.md) for detailed Mode-S documentation examples and reference material.

## Tool Restrictions

This skill can only **read** files - it won't modify your code. After review, I can help you implement suggested changes if you approve them.

# Product

<!-- impeccable:product-schema 1 -->

## Platform

web (the homepage). The product itself is a native Windows desktop app (WinForms, .NET 10).

## Stack

Homepage: static HTML/CSS, deployed on Vercel. App: C# WinForms, installer built with Inno Setup.

## Users

Everyday Windows PC users — students and office workers — who have taken several screenshots or copied several images and need them as one PDF (to submit an assignment, keep receipts, share documents). Not assumed to be technical.

## Product Purpose

ClipBind PDF turns images on the clipboard into a single PDF in a few clicks. Success: someone who has been copying images gets a correctly ordered PDF without saving each image as a file first.

## Positioning

It reads the Windows clipboard history (Win+V) directly, so images copied earlier — not just the last one — can be pulled in all at once, in the order they were copied. This is the lead selling point (confirmed by the user). Auto-add while copying is the supporting mechanism.

## Operating Context

Windows 10 (1809+) / 11, 64-bit. Typical flow: Win+Shift+S or "copy image" repeatedly → open ClipBind PDF → Import clipboard history (or auto-add) → reorder/rotate → Create PDF. Clipboard history must be turned on in Windows for the import; the app links to the setting when it is off.

## Capabilities and Constraints

- Import Win+V clipboard history; auto-add copied images; paste; add/drop image files (PNG, JPEG, BMP, GIF, TIFF).
- Reorder by drag, rotate, remove with undo, preview.
- PDF: A4 (orientation per image, 10 mm margins) or page = image size; quality High / Standard / Small (JPEG 95/85/70).
- Settings: file name template ({date}, {time}, {pages}), save folder (default Downloads), save without asking, open after saving, theme, language.
- Japanese / English UI; light / dark; high-DPI and mixed-DPI aware.
- Runs entirely on the PC; images are not uploaded anywhere (the app has no network code).
- Free download from GitHub Releases. Installer is not code-signed, so SmartScreen may warn.
- License: undecided — do not state one.

## Brand Commitments

Name "ClipBind PDF". App icon: ClipBind.png / ClipBind.ico. The app's accent is Windows blue (#0067C0 light / #4CC2FF-ish dark) in a Fluent-style UI.

## Evidence on Hand

- App screenshots: docs/screenshot-en-dark.png, docs/screenshot-ja-light.png (sample images are generated placeholders).
- Release v1.0.0 with installer: https://github.com/Kou-256/ClipBind-PDF/releases/tag/v1.0.0
- No users, testimonials, download counts, or press exist. Do not fabricate any.

## Product Principles

1. The fastest path from "I copied things" to "I have a PDF".
2. Nothing leaves the PC.
3. Plain language for non-technical users, in both Japanese and English.
4. Native-feeling on Windows, readable on every display.

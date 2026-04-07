# TOOLMake

A collection of **practical utility tools** built to reduce repetitive work, verify data, and speed up small operational tasks.

This repository is not a single product.  
It is a toolbox of small projects created during development and live-service workflows, with a focus on:

- **automation**
- **validation**
- **data extraction**
- **internal utility tooling**
- **small server/network experiments**

## Overview

In real development and live-service environments, many problems are not huge system design issues.  
They are often small but repetitive tasks such as:

- repeating manual input or click sequences
- checking percentages or calculated values
- extracting specific data after DB recovery
- validating configuration or server connection data
- building quick internal viewers for generated files
- testing simple client-server communication flows

This repository exists to solve those problems in a more practical way.

## Goals

The main goals of this repository are:

- Reduce repetitive manual work
- Lower the chance of human error
- Improve speed in small operational workflows
- Build simple tools for validation and investigation
- Create lightweight utilities instead of overengineering

## Projects

### `AutoKey`
A utility project for automating repetitive input-based tasks.  
Built as a lightweight helper for reducing manual actions in repetitive workflows.

### `AutoKey_v2`
An improved or extended version of `AutoKey`.  
Used to refine the previous approach and test a better structure or usage flow.

### `ChatServer`
A small **chat server / client** example project.  
Created to test and understand basic client-server communication, message flow, and simple network behavior.

### `ConnnectTool2`
A connection helper tool that works with configuration files and server lists such as:

- `config_template.txt`
- `servers.csv`

Its purpose is to simplify repeated connection-related tasks across multiple environments or server targets.

### `FindProj`
A Node.js-based project that extracts files and presents results in HTML format.  
Built as a lightweight internal-style viewer for quickly checking generated or collected file-based data.

### `PercentCheckTool`
A small utility for validating percentage-based or probability-based values.  
Useful in cases where incorrect numeric settings can directly affect behavior or balance.

### `sqldb복구후 자동추출툴`
A helper tool for automatically extracting required data after SQL DB recovery.  
Built to reduce repeated post-recovery processing work and improve consistency.

## Tech Stack

This repository contains multiple small projects, so the tech stack is mixed depending on the tool.

- **C#**
- **JavaScript / Node.js**
- **HTML**
- **C++**
- **Python**
- **T-SQL**

## What this repository represents

This repository reflects how I approach engineering problems in practice:

- solve real and repeated problems with small tools
- automate work when manual repetition is wasteful
- build validation tools when mistakes are expensive
- prefer practical usefulness over unnecessary complexity
- treat tooling as part of engineering productivity

## Notes

- Most projects here are **small, purpose-driven utilities**
- Some tools are experimental or prototype-style projects
- Only public-safe code and content are included
- Internal company logic, production data, and private operational details are not included

## Future Improvements

Planned improvements for this repository:

- Add a dedicated README for each subproject
- Document how to run each tool
- Add screenshots or sample input/output
- Clarify the problem each tool solves and the expected result
- Reorganize project folders for better readability

## Summary

Even small tools can have real engineering value.

This repository is focused on building practical utilities that help with:

- faster workflows
- safer operations
- repeated task reduction
- lightweight validation and investigation

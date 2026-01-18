# CodeIndex

CodeIndex is a lightweight symbol indexing tool for Java and C++ source code.

## Features
- Alphabetical identifier index
- Line-number tracking
- Configurable stop lists
- Comment-aware parsing
- Deterministic output

## Usage
```bash
java -jar CodeIndex.jar --lang java SourceFile.java
```

## Complexity
O(n + u log u)

## Design Philosophy
- Minimal grammar assumptions
- Language independence
- Pipeline-based architecture
- Deterministic, testable behavior

## Status
Stable academic reference implementation (v2.0)

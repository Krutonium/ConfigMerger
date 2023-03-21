# ConfigMerger

A simple utility to merge config files together - PR's welcome!

## Goals

I want to be able to take any arbitrary file type, on any OS where the code runs, and merge them together into a single file. The output should be a clean, merged version of the two, with the second file seen as a source of "Truth", overwriting the values in the first file.

## Why does this exist?

I wanted to be able to generate configuration on NixOS, via Nix, and then merge it onto config files that programs themselves manipulate, without adding those files to the store directly. Reason being there's a number of programs that get very unhappy if the config file is symlinked from the read only store.

## Supported File Types:

Currently:

 - XML
 - INI
 - JSON

 License: GPLv3

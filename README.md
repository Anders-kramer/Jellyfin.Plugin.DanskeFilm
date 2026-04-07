# Jellyfin.Plugin.DanskeFilm

A Jellyfin metadata plugin for Danish movies and actors using data from danskefilm.dk.

## Features

- Movie metadata (title, year, overview, genres, studios, writers, director)
- Full cast with roles
- Actor metadata (biography, birth/death, filmography)
- Movie images (poster + backdrops)
- Actor images
- Trailer links (when available)

## Data source

All metadata is sourced from:
https://www.danskefilm.dk/

## Current status

Work in progress, but already supports:

- Movie lookup via DanskeFilm ID
- Actor metadata via linked cast
- Image providers for movies and persons

## Installation (manual)

1. Build the plugin.

2. Copy the compiled DLL to your Jellyfin plugins folder.

3. Restart Jellyfin.

## Notes

- Search scoring is currently basic
- Person search is not implemented yet
- Best results when movies already match Danish titles

## Roadmap

- Better search matching
- Person search support
- TV series support
- Packaging for plugin catalog

## Disclaimer

This plugin is not affiliated with danskefilm.dk.

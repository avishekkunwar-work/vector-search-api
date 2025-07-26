# Installation Guide

1. Install Postgres Extension pgvector
Add the pgvector extension to your PostgreSQL database to enable vector similarity searches needed for embedding operations. Follow the instructions at https://github.com/pgvector/pgvector#installation.

2. Install Ollama
Download and install Ollama from https://ollama.com/. Once installed, use it to download the nomic-embed-text model from the Ollama Library. Ollama runs locally, so you can use it offline without an internet connection after setup.

3. Extract OpenAI API Key (using GPT-4o)
Obtain your OpenAI API key to authenticate requests to GPT-4o. This key is required for calling OpenAI’s services in your project.

4. Restore .NET Packages and Debug
Restore all .NET dependencies for the project and run a build/debug to ensure everything is set up correctly.

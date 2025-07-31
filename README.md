# Installation Guide

1. Install Postgres Extension pgvector
Add the pgvector extension to your PostgreSQL database to enable vector similarity searches needed for embedding operations. Follow the instructions at https://github.com/pgvector/pgvector#installation.

2. Install Ollama
Download and install Ollama from https://ollama.com/. Once installed, use it to download the nomic-embed-text model from the Ollama Library. Ollama runs locally, so you can use it offline without an internet connection after setup.

4. I'm utilizing the Gemma 2B model locally to handle embeddings and inference directly on my machine. This eliminates the need for API keys and external calls to cloud services like OpenAI. (Why? Because I prefer local processing for better data control, and Gemma 2B offers a lightweight yet capable solution without relying on GPU-intensive models like DeepSeek-R1.)

For Cloud Computing:
Obtain your OpenAI API key to authenticate requests to GPT-4o. This key is required for calling OpenAI’s services in your project.
( Why? Because my GPU is limited and deepseek-r1 is slow locally, I extract my OpenAI API key and use GPT-4o embeddings through the cloud for faster performance. )


FROM mcr.microsoft.com/devcontainers/base:ubuntu

# Install MongoDB
RUN apt-get update && apt-get install -y \
    mongodb-org \
    && rm -rf /var/lib/apt/lists/*

# Create MongoDB data directory
RUN mkdir -p /data/db && chown -R vscode:vscode /data/db

# Start MongoDB service on container start
RUN echo "#!/bin/bash" > /usr/local/bin/start-mongo.sh && \
    echo "mongod --dbpath /data/db --fork --logpath /tmp/mongodb.log" >> /usr/local/bin/start-mongo.sh && \
    chmod +x /usr/local/bin/start-mongo.sh

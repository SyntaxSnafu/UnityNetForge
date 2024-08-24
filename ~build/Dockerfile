FROM debian

# Install necessary packages
RUN apt-get update && apt-get install -y \
    libicu-dev && \
    rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

#copy your code into container
COPY ./code /app

#Set environment variable to enable globalization support
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

#Command to run your application
CMD ["./Promul.Relay.Server"]
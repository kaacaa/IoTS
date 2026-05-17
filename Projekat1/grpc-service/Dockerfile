FROM python:3.11-slim
WORKDIR /app
COPY requirements.txt .
RUN pip install -r requirements.txt
COPY proto/ ./proto/
COPY src/ ./src/
RUN mkdir -p proto_gen && \
    python -m grpc_tools.protoc \
    -I./proto \
    --python_out=./proto_gen \
    --grpc_python_out=./proto_gen \
    proto/sensor.proto && \
    touch proto_gen/__init__.py
EXPOSE 50051
CMD ["python", "src/server.py"]
import psycopg2.pool
import os

pool = psycopg2.pool.SimpleConnectionPool(
    1, 10,
    host=os.getenv("DB_HOST", "postgres"),
    port=os.getenv("DB_PORT", 5432),
    database=os.getenv("DB_NAME", "iotdb"),
    user=os.getenv("DB_USER", "iotuser"),
    password=os.getenv("DB_PASS", "iotpass")
)

def get_conn():
    return pool.getconn()

def put_conn(conn):
    pool.putconn(conn)
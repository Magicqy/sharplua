local endPoint = "minio.taiyouxi.net"
local key = "EHSDL6VYKW2VWALSX43W"
local secret = "lI00bo1khDQNWOHYJEMzewqWAWvNmEtVx5L+D4bb"

sharplua.MinIO_Init(endPoint, key, secret)

print("upload start")
local task = sharplua.MinIO_UploadFileAsync("test", "../sharplua.exe", "sharplua.exe")
sharplua.TaskWait(task)
print("upload done")

sharplua.MinIO_Dispose()
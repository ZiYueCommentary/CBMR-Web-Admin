// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Buffers.Binary;
using MessagePack;

namespace CbmrWebAdmin.Shared;

public static class PipeProtocol
{
    public const int MaxFrameLength = 16 * 1024 * 1024;

    public static async ValueTask WriteAsync(
        Stream stream,
        PipeEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        byte[] body = MessagePackSerializer.Serialize(envelope, cancellationToken: cancellationToken);
        if (body.Length > MaxFrameLength)
        {
            throw new InvalidDataException($"Pipe frame length {body.Length} exceeds the limit {MaxFrameLength}.");
        }

        byte[] header = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async ValueTask<PipeEnvelope?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[sizeof(int)];
        int headerLength = await ReadFramePartAsync(stream, header, allowEndOfStream: true, cancellationToken);
        if (headerLength == 0)
        {
            return null;
        }

        int bodyLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (bodyLength is <= 0 or > MaxFrameLength)
        {
            throw new InvalidDataException($"Invalid pipe frame length: {bodyLength}.");
        }

        byte[] body = new byte[bodyLength];
        await ReadFramePartAsync(stream, body, allowEndOfStream: false, cancellationToken);

        PipeEnvelope envelope = MessagePackSerializer.Deserialize<PipeEnvelope>(body, cancellationToken: cancellationToken);
        if (envelope.Version != PipeEnvelope.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported pipe protocol version {envelope.Version}; expected {PipeEnvelope.CurrentVersion}.");
        }

        return envelope;
    }

    private static async ValueTask<int> ReadFramePartAsync(
        Stream stream,
        Memory<byte> buffer,
        bool allowEndOfStream,
        CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(buffer[totalRead..], cancellationToken);
            if (bytesRead == 0)
            {
                if (allowEndOfStream && totalRead == 0)
                {
                    return 0;
                }

                throw new EndOfStreamException("The pipe closed while a frame was being read.");
            }

            totalRead += bytesRead;
        }

        return totalRead;
    }
}
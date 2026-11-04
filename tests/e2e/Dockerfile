# A minimal SSH server for the end-to-end test (scripts/e2e-docker.ps1): key login for "tunnel", TCP forwarding allowed.
FROM alpine:3
RUN apk add --no-cache openssh \
 && ssh-keygen -A \
 && adduser -D -s /bin/sh tunnel \
 && sed -i 's/^tunnel:!/tunnel:*/' /etc/shadow \
 && mkdir -p /home/tunnel/.ssh \
 && printf 'AllowTcpForwarding yes\nPasswordAuthentication no\nPermitRootLogin no\n' > /etc/ssh/sshd_config.d/thixtunnel.conf \
 && sed -i 's/^AllowTcpForwarding no/#AllowTcpForwarding no/' /etc/ssh/sshd_config
COPY authorized_keys /home/tunnel/.ssh/authorized_keys
RUN chown -R tunnel:tunnel /home/tunnel/.ssh && chmod 700 /home/tunnel/.ssh && chmod 600 /home/tunnel/.ssh/authorized_keys
EXPOSE 22
CMD ["/usr/sbin/sshd", "-D", "-e"]

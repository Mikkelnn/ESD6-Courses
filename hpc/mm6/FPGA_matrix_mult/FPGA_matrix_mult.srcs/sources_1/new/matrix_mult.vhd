library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity MatrixMultiplierParallel is
    generic (N : integer := 4);
    port (
        clk     : in std_logic;
        start   : in std_logic;
        done    : out std_logic;
        A, B    : in  std_logic_vector((N*N*8)-1 downto 0);
        C       : out std_logic_vector((N*N*16)-1 downto 0)
    );
end MatrixMultiplierParallel;

architecture FullyParallel of MatrixMultiplierParallel is
    type Matrix is array (0 to N-1, 0 to N-1) of std_logic_vector(15 downto 0);
    signal tempC : Matrix := (others => (others => (others => '0')));
    signal computing : std_logic := '0';
    signal done_internal : std_logic := '0';

begin
    process(clk)
    begin
        if rising_edge(clk) then
            if start = '1' then
                computing <= '1';
                done_internal <= '0';
                tempC <= (others => (others => (others => '0')));
            elsif computing = '1' then
                for i in 0 to N-1 loop
                    for j in 0 to N-1 loop
                        tempC(i, j) <= (others => '0');
                        for k in 0 to N-1 loop
                            tempC(i, j) <= tempC(i, j) + (A(((i*N+k)*8+7) downto (i*N+k)*8) * B(((k*N+j)*8+7) downto (k*N+j)*8));
                        end loop;
                    end loop;
                end loop;
                computing <= '0';
                done_internal <= '1';
            end if;
        end if;
    end process;
    
    -- Flatten output matrix C
    process(tempC)
    begin
        for i in 0 to N-1 loop
            for j in 0 to N-1 loop
                C(((i*N+j)*16+15) downto (i*N+j)*16) <= tempC(i, j);
            end loop;
        end loop;
    end process;
    
    done <= done_internal;
    
end FullyParallel;
